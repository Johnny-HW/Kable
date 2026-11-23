namespace Kable.Engine;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Exceptions;
using Kable.Observability;

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

    public SessionLifecycleState State
    {
        get
        {
            var state = (SessionLifecycleState)Volatile.Read(ref _lifecycleState);
            return state == SessionLifecycleState.Running && Volatile.Read(ref _isConnected) == 0
                ? SessionLifecycleState.Stopping : state;
        }
    }

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
                if (Volatile.Read(ref _isConnected) == 0)
                    throw new InvalidOperationException("Disconnected sessions cannot restart. Dispose this session and create a new session.");
                return;
            }
            if (currentState == SessionLifecycleState.Starting)
            {
                isStarter = false;
            }
            else if (currentState == SessionLifecycleState.Created)
            {
                Volatile.Write(ref _lifecycleState, (int)SessionLifecycleState.Starting);
                _healthTracker.SetState(DeviceConnectionState.Connecting);
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
            try
            {
                await WaitWithCancellationAsync(_startTcs.Task, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && _startTcs.Task.IsCompleted)
            {
                // Cleanup cancellation must not hide the already-published start outcome.
                await _startTcs.Task.ConfigureAwait(false);
            }
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

                bool isSimulated = context.EndpointDescription?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true
                    || context.EndpointDescription?.Contains("Mock", StringComparison.OrdinalIgnoreCase) == true
                    || context.EndpointDescription?.Contains("Simulat", StringComparison.OrdinalIgnoreCase) == true;
                _healthTracker.SetState(isSimulated ? DeviceConnectionState.Simulated : DeviceConnectionState.Connected);

                _outboundPumpTask = Task.Run(OutboundPumpLoopAsync);
                _deliveryTask = _delivery.RunAsync(_sessionCts.Token);
                _readLoopTask = Task.Run(ReadLoopAsync);

                _alarmSpool.Start(_sessionCts.Token);

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
                catch (Exception disposeEx) when (disposeEx is not OutOfMemoryException)
                {
                    System.Diagnostics.Debug.WriteLine($"[KableSession] Orphan context dispose failed: {disposeEx.Message}");
                }

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

}
