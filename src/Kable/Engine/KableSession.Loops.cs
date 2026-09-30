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

        try
        {
            while (!token.IsCancellationRequested)
            {
                OutboundCommand cmd;

                // 1. Check Urgent Queue first
                if (!_outboundUrgentQueue.Reader.TryRead(out cmd!))
                {
                    // If no urgent, wait for whichever comes first
                    var urgentWait = _outboundUrgentQueue.Reader.WaitToReadAsync(token).AsTask();
                    var normalWait = _outboundNormalQueue.Reader.WaitToReadAsync(token).AsTask();

                    var readyTask = await Task.WhenAny(urgentWait, normalWait).ConfigureAwait(false);
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
        }
    }

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

                while (_codec.TryDecode(ref buffer, out var message))
                {
                    Volatile.Write(ref _lastInboundTicks, DateTime.UtcNow.Ticks);
                    await _dispatchQueue.Writer.WriteAsync(message, token).ConfigureAwait(false);
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
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                "READ_LOOP_FAULT", ReadOnlyMemory<byte>.Empty,
                $"{ex.GetType().Name}: {ex.Message}", TimeSpan.Zero, LogLevel.Error));
            OnConnectionClosed();
        }
        finally
        {
            _dispatchQueue.Writer.TryComplete();
            OnConnectionClosed();
        }
    }

    private async Task DispatchLoopAsync()
    {
        var reader = _dispatchQueue.Reader;

        try
        {
            while (await reader.WaitToReadAsync(_sessionCts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var message))
                {
                    try
                    {
                        await DispatchMessageAsync(message).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _observer?.OnPacketTrace(new PacketTraceRecord(
                            DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                            "DISPATCH_MESSAGE_FAULT", ReadOnlyMemory<byte>.Empty,
                            $"{ex.GetType().Name}: {ex.Message}", TimeSpan.Zero, LogLevel.Error));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cooperative cancellation
        }
        catch (Exception ex)
        {
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                "DISPATCH_LOOP_FAULT", ReadOnlyMemory<byte>.Empty,
                $"{ex.GetType().Name}: {ex.Message}", TimeSpan.Zero, LogLevel.Error));
        }
        finally
        {
            while (reader.TryRead(out var residualMessage))
            {
                try
                {
                    await DispatchMessageAsync(residualMessage).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                        "DRAIN_DISPATCH_FAULT", ReadOnlyMemory<byte>.Empty,
                        $"{ex.GetType().Name}: {ex.Message}", TimeSpan.Zero, LogLevel.Warning));
                }
            }
        }
    }

    private async ValueTask DispatchMessageAsync(TMessage message)
    {
        Volatile.Write(ref _lastInboundTicks, DateTime.UtcNow.Ticks);

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
            await EnqueueIncomingMessageAsync(message).ConfigureAwait(false);
            return;
        }

        // Correlation Matching First
        if (_codec.SupportsCorrelationId)
        {
            var cid = _codec.ExtractCorrelationId(message);
            if (cid != null && _pendingRequests.TryRemove(cid, out var tcs))
            {
                _observer?.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Rx, TrafficKind.AperiodicCommand,
                    "RECV_CID", ReadOnlyMemory<byte>.Empty, message?.ToString(), TimeSpan.Zero));
                tcs.TrySetResult(message);
                return;
            }
        }
        else
        {
            if (_currentFifoTcs != null && !_currentFifoTcs.Task.IsCompleted)
            {
                _observer?.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Rx, TrafficKind.AperiodicCommand,
                    "RECV_FIFO", ReadOnlyMemory<byte>.Empty, message?.ToString(), TimeSpan.Zero));
                _currentFifoTcs.TrySetResult(message);
                return;
            }
        }

        // Fallback to incoming stream
        await EnqueueIncomingMessageAsync(message).ConfigureAwait(false);
    }

    private async Task HeartbeatLoopAsync()
    {
        if (_heartbeatOptions == null) return;
        var checkInterval = TimeSpan.FromMilliseconds(Math.Max(100, _heartbeatOptions.Interval.TotalMilliseconds / 2));

        try
        {
            while (!_sessionCts.Token.IsCancellationRequested)
            {
                await Task.Delay(checkInterval, _sessionCts.Token).ConfigureAwait(false);

                var lastTicks = Volatile.Read(ref _lastInboundTicks);
                var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - lastTicks);

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

    /// <summary>
    /// Background worker loop for persisting overflowed alarms to external storage.
    /// Decouples persistence I/O from the inbound dispatch loop to prevent blocking normal request-response routing.
    /// Passes a CancellationToken with AlarmSpoolTimeout to the callback.
    /// Uses a single overall drain timeout on shutdown and tracks Stored, InDoubt, and Unprocessed alarm counts.
    /// </summary>
    private async Task AlarmSpoolWorkerLoopAsync()
    {
        if (_alarmSpoolQueue == null || _sessionOptions.OnAlarmOverflowAsync == null) return;

        var token = _sessionCts.Token;
        var reader = _alarmSpoolQueue.Reader;

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!await reader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    break;
                }

                while (reader.TryRead(out var msg))
                {
                    bool success;
                    using var spoolCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    if (_sessionOptions.AlarmSpoolTimeout > TimeSpan.Zero)
                    {
                        spoolCts.CancelAfter(_sessionOptions.AlarmSpoolTimeout);
                    }

                    try
                    {
                        success = await _sessionOptions.OnAlarmOverflowAsync(msg, spoolCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        // 세션 종료(Stop/Dispose)에 의한 저장 중 취소: 진행 중이던 메시지는 '저장 여부 불명(InDoubt)'으로 집계
                        Interlocked.Increment(ref _spoolInDoubtCount);
                        _observer?.OnPacketTrace(new PacketTraceRecord(
                            DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                            "ALARM_SPOOL_IN_DOUBT", ReadOnlyMemory<byte>.Empty,
                            $"Alarm storage was canceled mid-flight during session shutdown for alarm ({msg}). Marked as In-Doubt.",
                            TimeSpan.Zero, LogLevel.Warning));
                        return;
                    }
                    catch (OperationCanceledException) when (spoolCts.IsCancellationRequested)
                    {
                        // 저장소 처리 제한시간 초과: InDoubt로 집계 후 세션 페일패스트 중단
                        Interlocked.Increment(ref _spoolInDoubtCount);
                        _observer?.OnPacketTrace(new PacketTraceRecord(
                            DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                            "ALARM_SPOOL_TIMEOUT", ReadOnlyMemory<byte>.Empty,
                            $"Alarm persistence timed out after {_sessionOptions.AlarmSpoolTimeout.TotalMilliseconds:F0}ms for alarm ({msg}). Aborting session.",
                            _sessionOptions.AlarmSpoolTimeout, LogLevel.Critical));

                        var timeoutEx = new AlarmBufferOverflowException(
                            $"Alarm storage persistence timed out after {_sessionOptions.AlarmSpoolTimeout.TotalMilliseconds:F0}ms for alarm ({msg}). Aborting session.");
                        _ = Task.Run(() => PerformCleanupAsync(timeoutEx));
                        return;
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref _spoolUnprocessedCount);
                        _observer?.OnPacketTrace(new PacketTraceRecord(
                            DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                            "ALARM_SPOOL_STORAGE_ERROR", ReadOnlyMemory<byte>.Empty,
                            $"Alarm storage persistence threw exception: {ex.Message}",
                            TimeSpan.Zero, LogLevel.Critical));

                        var abortEx = new AlarmBufferOverflowException(
                            $"Alarm storage persistence threw exception for alarm ({msg}). Aborting session to prevent silent loss.", ex);
                        _ = Task.Run(() => PerformCleanupAsync(abortEx));
                        return;
                    }

                    if (!success)
                    {
                        Interlocked.Increment(ref _spoolUnprocessedCount);
                        var abortEx = new AlarmBufferOverflowException(
                            $"Alarm storage handler reported failure persisting alarm ({msg}). Aborting session.");
                        _observer?.OnPacketTrace(new PacketTraceRecord(
                            DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                            "ALARM_SPOOL_SAVE_FAILED", ReadOnlyMemory<byte>.Empty, abortEx.Message,
                            TimeSpan.Zero, LogLevel.Critical));

                        _ = Task.Run(() => PerformCleanupAsync(abortEx));
                        return;
                    }

                    Interlocked.Increment(ref _spoolStoredCount);
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                        "ALARM_SPOOLED_TO_STORAGE", ReadOnlyMemory<byte>.Empty,
                        $"Spool worker successfully persisted alarm ({msg}) to external storage.",
                        TimeSpan.Zero, LogLevel.Information));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal session shutdown
        }
        catch (Exception ex)
        {
            _ = Task.Run(() => PerformCleanupAsync(ex));
        }
        finally
        {
            // 세션 종료 시 잔여 미저장 알람 배출 처리 (전체에 단 하나의 제한시간 AlarmSpoolDrainTimeout 적용)
            if (_sessionOptions.UnspooledAlarmDrainPolicy == UnspooledAlarmDrainPolicy.DrainWithinTimeout)
            {
                using var overallDrainCts = new CancellationTokenSource(_sessionOptions.AlarmSpoolDrainTimeout);
                var drainToken = overallDrainCts.Token;

                while (reader.TryRead(out var residualMsg))
                {
                    if (drainToken.IsCancellationRequested)
                    {
                        // 전체 배출 제한시간 초과 시 남은 큐 메시지는 즉시 미처리(Unprocessed)로 집계
                        Interlocked.Increment(ref _spoolUnprocessedCount);
                        continue;
                    }

                    try
                    {
                        bool drained = await _sessionOptions.OnAlarmOverflowAsync(residualMsg, drainToken).ConfigureAwait(false);
                        if (drained)
                        {
                            Interlocked.Increment(ref _spoolStoredCount);
                        }
                        else
                        {
                            Interlocked.Increment(ref _spoolUnprocessedCount);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 전체 배출 시간 만료로 취소된 메시지: InDoubt로 집계
                        Interlocked.Increment(ref _spoolInDoubtCount);
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref _spoolUnprocessedCount);
                    }
                }
            }
            else
            {
                // AbortImmediately 모드: 큐에 남은 모든 알람을 즉시 미처리(Unprocessed)로 집계
                while (reader.TryRead(out _))
                {
                    Interlocked.Increment(ref _spoolUnprocessedCount);
                }
            }

            // 최종 배출 결과 리포트 트레이스 기록
            var summary = SpoolSummary;
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                "ALARM_SPOOL_DRAIN_SUMMARY", ReadOnlyMemory<byte>.Empty,
                $"Spool worker terminated. Summary: Stored={summary.StoredCount}, InDoubt={summary.InDoubtCount}, Unprocessed={summary.UnprocessedCount}",
                TimeSpan.Zero, summary.UnprocessedCount > 0 || summary.InDoubtCount > 0 ? LogLevel.Warning : LogLevel.Information));
        }
    }
}
