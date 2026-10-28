namespace Kable.Engine;

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Exceptions;
using Kable.Observability;

public sealed partial class KableSession<TMessage>
{
    private async Task ReadLoopAsync()
    {
        var input = _context!.Input;
        var token = _sessionCts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var result = await input.ReadAsync(token).ConfigureAwait(false);
                var buffer = result.Buffer;

                while (_decoder.TryRead(ref buffer, out var message))
                {
                    Volatile.Write(ref _lastInboundTicks, _timeProvider.GetTimestamp());
                    _healthTracker.RecordRx(1);
                    try { await DispatchMessageAsync(message).ConfigureAwait(false); }
                    catch (KableException) { throw; }
                    catch (Exception error)
                    {
                        _observer?.OnPacketTrace(new PacketTraceRecord(DateTime.UtcNow, PacketDirection.Rx,
                            TrafficKind.SpontaneousAlarm, "DISPATCH_MESSAGE_FAULT", ReadOnlyMemory<byte>.Empty,
                            $"{error.GetType().Name}: {error.Message}", TimeSpan.Zero, LogLevel.Error));
                    }
                }

                input.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted || result.IsCanceled) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cooperative cancellation
        }
        catch (Exception ex)
        {
            _healthTracker.RecordError(1);
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                "READ_LOOP_FAULT", ReadOnlyMemory<byte>.Empty,
                $"{ex.GetType().Name}: {ex.Message}", TimeSpan.Zero, LogLevel.Error));
            OnConnectionClosed(ex is KableException ? ex : new DeviceDisconnectedException("Inbound processing failed.", ex));
        }
        finally
        {
            OnConnectionClosed();
        }
    }

    private async ValueTask DispatchMessageAsync(TMessage message)
    {
        Volatile.Write(ref _lastInboundTicks, _timeProvider.GetTimestamp());

        if (_heartbeatOptions?.IsPongResponse != null && _heartbeatOptions.IsPongResponse(message))
        {
            return;
        }

        // Autonomous / Unsolicited stream or Alarm check (Alarms must never be consumed as command responses)
        bool isAutonomous = _codec.IsAutonomousMessage(message);
        bool isAlarm = _sessionOptions.IsAlarmMessage != null
            ? _sessionOptions.IsAlarmMessage(message)
            : DefaultIsAlarm(message);

        if (isAutonomous || isAlarm)
        {
            await EnqueueIncomingMessageAsync(message, isAlarm).ConfigureAwait(false);
            return;
        }

        if (_responses.TryRoute(message)) return;

        // Fallback to incoming stream
        await EnqueueIncomingMessageAsync(message, isAlarm).ConfigureAwait(false);
    }

    private async Task HeartbeatLoopAsync()
    {
        if (_heartbeatOptions == null) return;
        var checkInterval = TimeSpan.FromMilliseconds(Math.Max(10, _heartbeatOptions.Interval.TotalMilliseconds / 2));

        try
        {
            while (!_sessionCts.Token.IsCancellationRequested)
            {
                var timeProvider = _heartbeatOptions.TimeProvider;
#if NET8_0_OR_GREATER
                await Task.Delay(checkInterval, timeProvider, _sessionCts.Token).ConfigureAwait(false);
#else
                await Task.Delay(checkInterval, _sessionCts.Token).ConfigureAwait(false);
#endif

                var lastTicks = Volatile.Read(ref _lastInboundTicks);
                var currentTimestamp = timeProvider.GetTimestamp();
                var elapsed = timeProvider.GetElapsedTime(lastTicks, currentTimestamp);

                if (elapsed > _heartbeatOptions.Timeout)
                {
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                        "HEARTBEAT_TIMEOUT", ReadOnlyMemory<byte>.Empty,
                        $"Heartbeat timeout. No inbound traffic for {elapsed.TotalMilliseconds:F0}ms (limit: {_heartbeatOptions.Timeout.TotalMilliseconds:F0}ms).",
                        elapsed, LogLevel.Critical));

                    OnConnectionClosed();
                    break;
                }

                // Send Ping via Outbound queue
                try
                {
                    var pingMsg = _heartbeatOptions.PingFactory();
                    var cmd = new OutboundCommand(pingMsg, isUrgent: false);
                    if (_outboundNormalQueue.Writer.TryWrite(cmd))
                    {
                        await cmd.Completion.Task.ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

}
