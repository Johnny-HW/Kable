namespace Kable.Engine;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Exceptions;
using Kable.Observability;

public sealed partial class KableSession<TMessage>
{
    private readonly SemaphoreSlim _fifoLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TMessage>> _pendingRequests = new();
    private TaskCompletionSource<TMessage>? _currentFifoTcs;
    private long _fifoSequence;
    private long _activeFifoSeq;

    private readonly Channel<OutboundCommand> _outboundUrgentQueue = Channel.CreateUnbounded<OutboundCommand>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Channel<OutboundCommand> _outboundNormalQueue = Channel.CreateBounded<OutboundCommand>(new BoundedChannelOptions(5000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });

    private void EnsureConnected()
    {
        if (State != SessionLifecycleState.Running || Volatile.Read(ref _isConnected) == 0 || _context == null)
        {
            throw new DeviceDisconnectedException("Connection is not open. Call StartAsync() first.");
        }
    }

    public async ValueTask SendAsync(TMessage message, CancellationToken ct = default)
    {
        EnsureConnected();
        var cmd = new OutboundCommand(message, isUrgent: false, ct);
        try
        {
            await _outboundNormalQueue.Writer.WriteAsync(cmd, ct).ConfigureAwait(false);
            await WaitWithCancellationAsync(cmd.Completion.Task, ct).ConfigureAwait(false);
        }
        catch (ChannelClosedException cce)
        {
            throw new DeviceDisconnectedException("Hardware connection has been disconnected.", cce);
        }

        _observer?.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow, PacketDirection.Tx, TrafficKind.AperiodicCommand,
            "SEND", ReadOnlyMemory<byte>.Empty, message?.ToString(), TimeSpan.Zero, LogLevel.Debug));
    }

    public async ValueTask<TResponse> RequestAsync<TResponse>(TMessage request, TimeSpan timeout, CancellationToken ct = default)
    {
        EnsureConnected();

        bool hasCid = _codec.SupportsCorrelationId;
        string? cid = hasCid ? _codec.ExtractCorrelationId(request) : null;

        if (string.IsNullOrEmpty(cid))
        {
            // Strict FIFO Mode
            long currentSeq = Interlocked.Increment(ref _fifoSequence);
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var effectiveToken = linkedCts.Token;

            try
            {
                await _fifoLock.WaitAsync(effectiveToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    throw new DeviceTimeoutException(request?.ToString() ?? "UnknownCommand", timeout);
                }
                throw;
            }

            try
            {
                EnsureConnected();
                Volatile.Write(ref _activeFifoSeq, currentSeq);

                var tcs = new TaskCompletionSource<TMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                _currentFifoTcs = tcs;

                var cmd = new OutboundCommand(request, isUrgent: false, effectiveToken);
                try
                {
                    await _outboundNormalQueue.Writer.WriteAsync(cmd, effectiveToken).ConfigureAwait(false);
                    await WaitWithCancellationAsync(cmd.Completion.Task, effectiveToken).ConfigureAwait(false);
                }
                catch (ChannelClosedException cce)
                {
                    throw new DeviceDisconnectedException("Hardware connection has been disconnected.", cce);
                }

                var responseMsg = await WaitWithCancellationAsync(tcs.Task, effectiveToken).ConfigureAwait(false);

                if (responseMsg is TResponse typedResp)
                {
                    return typedResp;
                }

                if (typeof(TResponse) == typeof(string))
                {
                    return (TResponse)(object)(responseMsg?.ToString() ?? string.Empty);
                }

                throw new InvalidCastException($"Cannot cast response of type '{typeof(TMessage).Name}' to requested '{typeof(TResponse).Name}'.");
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                        "DEVICE_TIMEOUT", ReadOnlyMemory<byte>.Empty,
                        $"Command '{request}' timed out after {timeout.TotalMilliseconds}ms.",
                        timeout, LogLevel.Warning));

                    OnConnectionClosed();
                    throw new DeviceTimeoutException(request?.ToString() ?? "UnknownCommand", timeout);
                }

                OnConnectionClosed();
                throw;
            }
            finally
            {
                Volatile.Write(ref _activeFifoSeq, 0);
                _currentFifoTcs = null;
                _fifoLock.Release();
            }
        }
        else
        {
            // Full-Duplex Multiplexed CID Mode
            var tcs = new TaskCompletionSource<TMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pendingRequests.TryAdd(cid!, tcs))
            {
                throw new InvalidOperationException($"Duplicate in-flight Correlation ID '{cid}' detected.");
            }

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var effectiveToken = linkedCts.Token;

            var cmd = new OutboundCommand(request, isUrgent: false, effectiveToken);
            try
            {
                await _outboundNormalQueue.Writer.WriteAsync(cmd, effectiveToken).ConfigureAwait(false);
                await WaitWithCancellationAsync(cmd.Completion.Task, effectiveToken).ConfigureAwait(false);

                var responseMsg = await WaitWithCancellationAsync(tcs.Task, effectiveToken).ConfigureAwait(false);

                if (responseMsg is TResponse typedResp)
                {
                    return typedResp;
                }

                if (typeof(TResponse) == typeof(string))
                {
                    return (TResponse)(object)(responseMsg?.ToString() ?? string.Empty);
                }

                throw new InvalidCastException($"Cannot cast response of type '{typeof(TMessage).Name}' to requested '{typeof(TResponse).Name}'.");
            }
            catch (ChannelClosedException cce)
            {
                throw new DeviceDisconnectedException("Hardware connection has been disconnected.", cce);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                _observer?.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                    "DEVICE_TIMEOUT", ReadOnlyMemory<byte>.Empty,
                    $"Command '{request}' timed out after {timeout.TotalMilliseconds}ms.",
                    timeout, LogLevel.Warning));

                throw new DeviceTimeoutException(request?.ToString() ?? "UnknownCommand", timeout);
            }
            finally
            {
                _pendingRequests.TryRemove(cid!, out _);
            }
        }
    }

    private static async Task<T> WaitWithCancellationAsync<T>(Task<T> task, CancellationToken ct)
    {
        if (task.IsCompleted)
        {
            return await task.ConfigureAwait(false);
        }

        var delayTask = Task.Delay(Timeout.Infinite, ct);
        var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
        if (completed == delayTask)
        {
            ct.ThrowIfCancellationRequested();
        }
        return await task.ConfigureAwait(false);
    }

    private static async Task WaitWithCancellationAsync(Task task, CancellationToken ct)
    {
        if (task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        var delayTask = Task.Delay(Timeout.Infinite, ct);
        var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
        if (completed == delayTask)
        {
            ct.ThrowIfCancellationRequested();
        }
        await task.ConfigureAwait(false);
    }

    public async ValueTask SendUrgentAsync(TMessage urgentMessage)
    {
        EnsureConnected();
        var cmd = new OutboundCommand(urgentMessage, isUrgent: true);
        if (!_outboundUrgentQueue.Writer.TryWrite(cmd))
        {
            throw new DeviceDisconnectedException("Failed to enqueue urgent command; hardware connection is terminating.");
        }
        await cmd.Completion.Task.ConfigureAwait(false);

        _observer?.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow, PacketDirection.Tx, TrafficKind.AperiodicCommand,
            "URGENT_OOB", ReadOnlyMemory<byte>.Empty, urgentMessage?.ToString(), TimeSpan.Zero, LogLevel.Critical));
    }

    private sealed class OutboundCommand
    {
        public TMessage Message { get; }
        public bool IsUrgent { get; }
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken CancellationToken { get; }
        public bool IsCanceled => CancellationToken.IsCancellationRequested;

        public OutboundCommand(TMessage message, bool isUrgent, CancellationToken cancellationToken = default)
        {
            Message = message;
            IsUrgent = isUrgent;
            CancellationToken = cancellationToken;
        }
    }
}
