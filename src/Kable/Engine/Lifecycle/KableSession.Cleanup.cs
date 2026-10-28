namespace Kable.Engine;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Exceptions;
using Kable.Observability;

public sealed partial class KableSession<TMessage>
{
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
            _alarmSpool.Complete();

            // 2. 일반 송수신 루프는 기존 종료 정책(고정 2초 타임아웃)으로 정리
            var tasksToWait = new List<Task>();
            if (_readLoopTask != null) tasksToWait.Add(_readLoopTask);
            if (_outboundPumpTask != null) tasksToWait.Add(_outboundPumpTask);
            if (_deliveryTask != null) tasksToWait.Add(_deliveryTask);
            if (_heartbeatTask != null) tasksToWait.Add(_heartbeatTask);

            if (tasksToWait.Count > 0)
            {
                var joinAllTask = Task.WhenAll(tasksToWait);
                var timeoutTask = Task.Delay(2000);
                await Task.WhenAny(joinAllTask, timeoutTask).ConfigureAwait(false);
            }

            // 3. _alarmSpool.Completion는 고정 2초에 묶지 않고 직접 await (배출 제한시간은 워커가 관리)
            Exception? workerException = null;
            try { await _alarmSpool.Completion.ConfigureAwait(false); }
            catch (Exception ex) { workerException = ex; }

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

                try { _responses.Dispose(); } catch { }
                try { _lifecycleLock.Dispose(); } catch { }
                try { _sessionCts.Dispose(); } catch { }
            }
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

}
