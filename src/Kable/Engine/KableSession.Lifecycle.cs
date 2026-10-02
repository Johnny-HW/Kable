namespace Kable.Engine;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Exceptions;

public enum SessionLifecycleState
{
    Created = 0,
    Starting = 1,
    Running = 2,
    Stopping = 3,
    Stopped = 4,
    Disposed = 5
}

public sealed partial class KableSession<TMessage>
{
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private int _lifecycleState = (int)SessionLifecycleState.Created;
    private int _isDisposed;
    private Exception? _disconnectReason;
    private readonly TaskCompletionSource<bool> _startTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _cleanupTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SessionLifecycleState State => (SessionLifecycleState)Volatile.Read(ref _lifecycleState);

    public async ValueTask StartAsync(CancellationToken ct = default)
    {
        if (_sessionOptions.AlarmOverflowMode == AlarmOverflowMode.SpoolToStorage && _sessionOptions.OnAlarmOverflowAsync == null)
        {
            throw new InvalidOperationException(
                "KableSession configuration error: AlarmOverflowMode is set to SpoolToStorage, but OnAlarmOverflowAsync callback is not provided.");
        }

        bool isStarter = false;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _sessionCts.Token);

        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var currentState = (SessionLifecycleState)Volatile.Read(ref _lifecycleState);
            if (currentState == SessionLifecycleState.Running)
            {
                return;
            }
            if (currentState == SessionLifecycleState.Starting)
            {
                isStarter = false;
            }
            else if (currentState == SessionLifecycleState.Created)
            {
                Volatile.Write(ref _lifecycleState, (int)SessionLifecycleState.Starting);
                isStarter = true;
            }
            else
            {
                throw new InvalidOperationException($"Cannot start KableSession in state '{currentState}'. Session has a single-life contract.");
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }

        // 최초 시작자가 아닌 모든 동시 호출자는 최초 시작자의 Task만 기다림 (중복 ConnectAsync 호출 완전 차단)
        if (!isStarter)
        {
            await _startTcs.Task.ConfigureAwait(false);
            return;
        }

        // ConnectAsync is executed outside the lifecycle lock so it does not block concurrent StopAsync
        IConnectionContext context;
        try
        {
            context = await _connectionFactory.ConnectAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _startTcs.TrySetException(ex);
            await PerformCleanupAsync(ex).ConfigureAwait(false);
            throw;
        }

        // Re-acquire lock to transition to Running and register context atomically
        await _lifecycleLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var currentState = (SessionLifecycleState)Volatile.Read(ref _lifecycleState);
            if (currentState == SessionLifecycleState.Starting)
            {
                _context = context;
                _context.ConnectionClosed.Register(OnConnectionClosed);
                Volatile.Write(ref _lastInboundTicks, _timeProvider.GetTimestamp());
                Volatile.Write(ref _isConnected, 1);
                Volatile.Write(ref _lifecycleState, (int)SessionLifecycleState.Running);

                _outboundPumpTask = Task.Run(OutboundPumpLoopAsync);
                _dispatchLoopTask = Task.Run(DispatchLoopAsync);
                _readLoopTask = Task.Run(ReadLoopAsync);

                if (_alarmSpoolQueue != null)
                {
                    _alarmSpoolWorkerTask = Task.Run(AlarmSpoolWorkerLoopAsync);
                }

                if (_heartbeatOptions != null)
                {
                    _heartbeatTask = Task.Run(HeartbeatLoopAsync);
                }

                _startTcs.TrySetResult(true);
            }
            else
            {
                // Stop or Dispose was called while connecting: immediately dispose orphan context
                try
                {
                    await context.DisposeAsync().ConfigureAwait(false);
                }
                catch { }

                Volatile.Write(ref _isConnected, 0);
                var cancelEx = new OperationCanceledException("Session was stopped or disposed while establishing connection.");
                _startTcs.TrySetException(cancelEx);
                throw cancelEx;
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public ValueTask StopAsync()
    {
        return new ValueTask(PerformCleanupAsync(reason: null));
    }

    private async Task PerformCleanupAsync(Exception? reason)
    {
        // 2. 이미 종료된 상태의 조기 return 제거
        // Dispose 이후 호출은 이미 폐기된 락을 만지지 않도록, 락 진입 전에 공유 Task로 연결합니다.
        // 또한 이미 _cleanupTcs가 완료된 경우(Stopped/Disposed)에도 조기 성공 반환하지 않고 공유 Task를 기다려 동일한 결과를 수신합니다.
        if (Volatile.Read(ref _isDisposed) == 1 || _cleanupTcs.Task.IsCompleted)
        {
            await _cleanupTcs.Task.ConfigureAwait(false);
            return;
        }

        bool isFirstStopper = false;
        await _lifecycleLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var currentState = (SessionLifecycleState)Volatile.Read(ref _lifecycleState);
            if (currentState == SessionLifecycleState.Disposed || currentState == SessionLifecycleState.Stopped)
            {
                // 이미 정리가 완료된 상태: 락 해제 후 아래에서 _cleanupTcs.Task 대기
            }
            else if (currentState == SessionLifecycleState.Stopping)
            {
                // 다른 호출자가 현재 정리 수행 중: 락 해제 후 아래에서 _cleanupTcs.Task 대기
            }
            else
            {
                // 최초 호출자!
                isFirstStopper = true;
                Volatile.Write(ref _lifecycleState, (int)SessionLifecycleState.Stopping);
                try { _sessionCts.Cancel(); } catch (ObjectDisposedException) { }
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }

        // 최초 호출자가 아니면 완료 Task를 기다리고 동일한 결과(성공 또는 예외)를 수신합니다.
        if (!isFirstStopper)
        {
            await _cleanupTcs.Task.ConfigureAwait(false);
            return;
        }

        // 1. 최초 호출자만 실제 정리를 수행합니다.
        try
        {
            // If StartAsync was in flight, wait for it to exit
            if (!_startTcs.Task.IsCompleted)
            {
                try
                {
                    await Task.WhenAny(_startTcs.Task, Task.Delay(2000)).ConfigureAwait(false);
                }
                catch { }
            }

            OnConnectionClosed(reason);

            // 1. 신규 스풀 등록 차단
            _alarmSpoolQueue?.Writer.TryComplete();

            // 2. 일반 송수신 루프는 기존 종료 정책(고정 2초 타임아웃)으로 정리
            var tasksToWait = new List<Task>();
            if (_readLoopTask != null) tasksToWait.Add(_readLoopTask);
            if (_outboundPumpTask != null) tasksToWait.Add(_outboundPumpTask);
            if (_dispatchLoopTask != null) tasksToWait.Add(_dispatchLoopTask);
            if (_heartbeatTask != null) tasksToWait.Add(_heartbeatTask);

            if (tasksToWait.Count > 0)
            {
                var joinAllTask = Task.WhenAll(tasksToWait);
                var timeoutTask = Task.Delay(2000);
                await Task.WhenAny(joinAllTask, timeoutTask).ConfigureAwait(false);
            }

            // 3. _alarmSpoolWorkerTask는 고정 2초에 묶지 않고 직접 await (배출 제한시간은 워커가 관리)
            Exception? workerException = null;
            if (_alarmSpoolWorkerTask != null)
            {
                try
                {
                    await _alarmSpoolWorkerTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    workerException = ex;
                }
            }

            // 4. 워커 종료(SpoolSummary 확정) 후 연결 리소스 폐기 (정확히 1회 폐기 보장)
            Exception? contextException = null;
            if (_context != null)
            {
                var ctx = _context;
                _context = null;
                try
                {
                    await ctx.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    contextException = ex;
                }
            }

            await _lifecycleLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                Volatile.Write(ref _lifecycleState, (int)SessionLifecycleState.Stopped);
                Volatile.Write(ref _isConnected, 0);
            }
            finally
            {
                _lifecycleLock.Release();
            }

            var finalException = workerException ?? contextException ?? reason;
            if (finalException != null)
            {
                _cleanupTcs.TrySetException(finalException);
            }
            else
            {
                _cleanupTcs.TrySetResult(true);
            }
        }
        catch (Exception ex)
        {
            _cleanupTcs.TrySetException(ex);
        }

        // 최초 호출자도 함수 마지막에서 await _cleanupTcs.Task를 수행!
        await _cleanupTcs.Task.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                Volatile.Write(ref _lifecycleState, (int)SessionLifecycleState.Disposed);

                try { _fifoLock.Dispose(); } catch { }
                try { _lifecycleLock.Dispose(); } catch { }
                try { _sessionCts.Dispose(); } catch { }
            }
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void OnConnectionClosed()
    {
        OnConnectionClosed(null);
    }

    private void OnConnectionClosed(Exception? specificReason)
    {
        if (Interlocked.Exchange(ref _isConnected, 0) == 1)
        {
            var ex = specificReason ?? new DeviceDisconnectedException("Hardware connection has been disconnected. (Fail-fast aborting all pending requests)");
            _disconnectReason = ex;

            _currentFifoTcs?.TrySetException(ex);
            foreach (var kvp in _pendingRequests)
            {
                kvp.Value.TrySetException(ex);
            }
            _pendingRequests.Clear();
            _alarmStream.Writer.TryComplete(ex);
            _telemetryStream.Writer.TryComplete(ex);
            _outboundNormalQueue.Writer.TryComplete(ex);
            _outboundUrgentQueue.Writer.TryComplete(ex);

            while (_outboundUrgentQueue.Reader.TryRead(out var pendingUrgent))
            {
                pendingUrgent.Completion.TrySetException(ex);
            }
            while (_outboundNormalQueue.Reader.TryRead(out var pendingNormal))
            {
                pendingNormal.Completion.TrySetException(ex);
            }

            try
            {
                _sessionCts.Cancel();
            }
            catch (ObjectDisposedException) { }

            try
            {
                _context?.Abort("Hardware connection has been disconnected.");
            }
            catch { }
        }
    }
}
