namespace Kable.Engine;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Exceptions;
using Kable.Observability;

public sealed partial class KableSession<TMessage>
{
    private readonly Messaging.ResponseRouter<TMessage> _responses;

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

        long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

        if (string.IsNullOrEmpty(cid))
        {
            // Strict FIFO Mode
            using var timeoutCts = CreateRequestDeadline(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var effectiveToken = linkedCts.Token;

            try
            {
                await _responses.WaitForFifoAsync(effectiveToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    _healthTracker.RecordError(1);
                    throw CreateTimeoutError(request, timeout);
                }
                throw;
            }

            bool sent = false;
            bool responseReceived = false;
            try
            {
                EnsureConnected();
                var tcs = _responses.BeginFifo();

                var cmd = new OutboundCommand(request, isUrgent: false, effectiveToken);
                try
                {
                    await _outboundNormalQueue.Writer.WriteAsync(cmd, effectiveToken).ConfigureAwait(false);
                    await WaitWithCancellationAsync(cmd.Completion.Task, effectiveToken).ConfigureAwait(false);
                    sent = true;

                    var effectiveKind = CommTrafficContext.CurrentKind;
                    TraceRequestPacket(request, PacketDirection.Tx, effectiveKind, "REQ_TX", TimeSpan.Zero);
                }
                catch (ChannelClosedException cce)
                {
                    _healthTracker.RecordError(1);
                    throw new DeviceDisconnectedException("Hardware connection has been disconnected.", cce);
                }

                var responseMsg = await WaitWithCancellationAsync(tcs.Task, effectiveToken).ConfigureAwait(false);
                responseReceived = true;
                var latency = _timeProvider.GetElapsedTime(startTimestamp);
                _healthTracker.RecordRttTicks(System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp);

                var respKind = CommTrafficContext.CurrentKind;
                TraceRequestPacket(responseMsg, PacketDirection.Rx, respKind, "RESP_RX", latency);

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
                    _healthTracker.RecordError(1);
                    OnConnectionClosed();
                    var timeoutError = CreateTimeoutError(request, timeout);
                    try
                    {
                        _observer?.OnPacketTrace(new PacketTraceRecord(
                            DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                            "DEVICE_TIMEOUT", ReadOnlyMemory<byte>.Empty,
                            $"Command '{timeoutError.Command}' timed out after {timeout.TotalMilliseconds}ms.",
                            timeout, LogLevel.Warning));
                    }
                    catch (Exception traceError)
                    {
                        timeoutError.Data["TraceException"] = traceError;
                    }
                    throw timeoutError;
                }

                OnConnectionClosed();
                throw;
            }
            catch
            {
                if (sent && !responseReceived) OnConnectionClosed();
                throw;
            }
            finally
            {
                _responses.EndFifo();
            }
        }
        else
        {
            // Full-Duplex Multiplexed CID Mode
            // Validate timeout and create cancellation resources before reserving a CID.
            using var timeoutCts = CreateRequestDeadline(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var effectiveToken = linkedCts.Token;

            var tcs = _responses.Reserve(cid!);

            var cmd = new OutboundCommand(request, isUrgent: false, effectiveToken);
            try
            {
                await _outboundNormalQueue.Writer.WriteAsync(cmd, effectiveToken).ConfigureAwait(false);
                await WaitWithCancellationAsync(cmd.Completion.Task, effectiveToken).ConfigureAwait(false);

                var effectiveKind = CommTrafficContext.CurrentKind;
                TraceRequestPacket(request, PacketDirection.Tx, effectiveKind, "REQ_TX", TimeSpan.Zero);

                var responseMsg = await WaitWithCancellationAsync(tcs.Task, effectiveToken).ConfigureAwait(false);
                var latency = _timeProvider.GetElapsedTime(startTimestamp);
                _healthTracker.RecordRttTicks(System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp);

                var respKind = CommTrafficContext.CurrentKind;
                TraceRequestPacket(responseMsg, PacketDirection.Rx, respKind, "RESP_RX", latency);

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
                _healthTracker.RecordError(1);
                throw new DeviceDisconnectedException("Hardware connection has been disconnected.", cce);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                _healthTracker.RecordError(1);
                var timeoutError = CreateTimeoutError(request, timeout);
                _observer?.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                    "DEVICE_TIMEOUT", ReadOnlyMemory<byte>.Empty,
                    $"Command '{timeoutError.Command}' timed out after {timeout.TotalMilliseconds}ms.",
                    timeout, LogLevel.Warning));

                throw timeoutError;
            }
            finally
            {
                _responses.Release(cid!, tcs);
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
