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
    /// <summary>
    /// Outbound Writer Pump Loop.
    /// Serializes access to PipeWriter, prioritizes urgent messages, and batches flush calls.
    /// </summary>
    private async Task OutboundPumpLoopAsync()
    {
        var output = _context!.Output;
        var token = _sessionCts.Token;
        Task<bool>? urgentWait = null;
        Task<bool>? normalWait = null;

        try
        {
            while (!token.IsCancellationRequested)
            {
                OutboundCommand cmd;

                // 1. Check Urgent Queue first
                if (!_outboundUrgentQueue.Reader.TryRead(out cmd!))
                {
                    // Keep the unselected wait instead of replacing a live channel waiter.
                    urgentWait ??= _outboundUrgentQueue.Reader.WaitToReadAsync(token).AsTask();
                    normalWait ??= _outboundNormalQueue.Reader.WaitToReadAsync(token).AsTask();

                    var readyTask = await Task.WhenAny(urgentWait, normalWait).ConfigureAwait(false);
                    if (ReferenceEquals(readyTask, urgentWait)) urgentWait = null;
                    else normalWait = null;
                    if (!await readyTask.ConfigureAwait(false))
                    {
                        break;
                    }

                    if (!_outboundUrgentQueue.Reader.TryRead(out cmd!) &&
                        !_outboundNormalQueue.Reader.TryRead(out cmd!))
                    {
                        continue;
                    }
                }

                // If primary command was canceled while waiting in queue, skip transmission completely
                if (cmd.IsCanceled)
                {
                    cmd.Completion.TrySetCanceled(cmd.CancellationToken);
                    continue;
                }

                // 2. Encode primary and batched messages inside safe bulkhead
                var commandsToComplete = new List<OutboundCommand>(8) { cmd };
                try
                {
                    _codec.Encode(cmd.Message, output);

                    // 3. Batching: drain any currently pending messages before triggering syscall flush
                    while (_outboundUrgentQueue.Reader.TryRead(out var queuedUrgent))
                    {
                        if (queuedUrgent.IsCanceled)
                        {
                            queuedUrgent.Completion.TrySetCanceled(queuedUrgent.CancellationToken);
                            continue;
                        }
                        commandsToComplete.Add(queuedUrgent);
                        _codec.Encode(queuedUrgent.Message, output);
                    }

                    while (commandsToComplete.Count < 32 && _outboundNormalQueue.Reader.TryRead(out var queuedNormal))
                    {
                        if (queuedNormal.IsCanceled)
                        {
                            queuedNormal.Completion.TrySetCanceled(queuedNormal.CancellationToken);
                            continue;
                        }
                        commandsToComplete.Add(queuedNormal);
                        _codec.Encode(queuedNormal.Message, output);
                    }
                }
                catch (Exception encodeEx)
                {
                    Exception translatedEx = encodeEx;
                    if (encodeEx is InvalidOperationException ||
                        encodeEx is System.IO.IOException ||
                        encodeEx.InnerException is System.Net.Sockets.SocketException ||
                        encodeEx.InnerException is System.IO.IOException)
                    {
                        translatedEx = new DeviceDisconnectedException("Transport disconnected or closed during encoding/writing.", encodeEx);
                    }

                    foreach (var c in commandsToComplete)
                    {
                        c.Completion.TrySetException(translatedEx);
                    }
                    throw;
                }

                // 4. Single consolidated FlushAsync
                try
                {
                    var flushResult = await output.FlushAsync(token).ConfigureAwait(false);
                    if (flushResult.IsCanceled)
                    {
                        var discEx = new DeviceDisconnectedException("Hardware connection closed during outbound flush.");
                        foreach (var c in commandsToComplete)
                        {
                            c.Completion.TrySetException(discEx);
                        }
                        break;
                    }

                    if (flushResult.IsCompleted)
                    {
                        var discEx = new DeviceDisconnectedException("Transport output pipe completed during flush.");
                        foreach (var c in commandsToComplete)
                        {
                            c.Completion.TrySetException(discEx);
                        }
                        break;
                    }

                    foreach (var c in commandsToComplete)
                    {
                        c.Completion.TrySetResult(true);
                    }
                    _healthTracker.RecordTx(commandsToComplete.Count);
                }
                catch (OperationCanceledException ocex)
                {
                    var discEx = new DeviceDisconnectedException("Hardware connection closed during outbound flush.", ocex);
                    foreach (var c in commandsToComplete)
                    {
                        c.Completion.TrySetException(discEx);
                    }
                    break;
                }
                catch (Exception flushEx)
                {
                    Exception translatedEx = flushEx;
                    if (flushEx is InvalidOperationException ||
                        flushEx is System.IO.IOException ||
                        flushEx.InnerException is System.Net.Sockets.SocketException ||
                        flushEx.InnerException is System.IO.IOException)
                    {
                        translatedEx = new DeviceDisconnectedException("Transport disconnected during outbound flush.", flushEx);
                    }

                    foreach (var c in commandsToComplete)
                    {
                        c.Completion.TrySetException(translatedEx);
                    }
                    throw;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cooperative exit
        }
        catch (Exception ex)
        {
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Tx, TrafficKind.SpontaneousAlarm,
                "IO_FLUSH_ERROR", ReadOnlyMemory<byte>.Empty, ex.Message, TimeSpan.Zero, LogLevel.Error));
        }
        finally
        {
            OnConnectionClosed();
            await ObserveOutboundWaitAsync(urgentWait).ConfigureAwait(false);
            await ObserveOutboundWaitAsync(normalWait).ConfigureAwait(false);
        }
    }

    private async Task ObserveOutboundWaitAsync(Task<bool>? wait)
    {
        if (wait == null) return;
        try
        {
            await wait.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_sessionCts.IsCancellationRequested)
        {
            // Expected when the session closes while the other queue is idle.
        }
        catch (Exception ex)
        {
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Tx, TrafficKind.SpontaneousAlarm,
                "OUTBOUND_WAIT_FAULT", ReadOnlyMemory<byte>.Empty, ex.Message, TimeSpan.Zero, LogLevel.Error));
        }
    }

}
