namespace Kable.Engine;

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Exceptions;
using Kable.Observability;

public sealed partial class KableSession<TMessage>
{
    private readonly Channel<TMessage> _alarmStream;
    private readonly Channel<TMessage> _telemetryStream;
    private long _droppedTelemetryCount;

    public long DroppedTelemetryCount => Volatile.Read(ref _droppedTelemetryCount);

    private void CheckChannelFault()
    {
        if (_disconnectReason != null)
        {
            throw _disconnectReason;
        }
        if (_alarmStream.Reader.Completion.IsFaulted)
        {
            var ex = _alarmStream.Reader.Completion.Exception?.GetBaseException();
            if (ex != null) throw ex;
        }
        if (_telemetryStream.Reader.Completion.IsFaulted)
        {
            var ex = _telemetryStream.Reader.Completion.Exception?.GetBaseException();
            if (ex != null) throw ex;
        }
    }

    public async IAsyncEnumerable<TMessage> GetStreamAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var enumerator = GetStreamInternalAsync(ct).GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                bool hasMore;
                try
                {
                    hasMore = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    CheckChannelFault();
                    throw;
                }

                if (!hasMore) break;
                yield return enumerator.Current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        CheckChannelFault();
    }

    private async IAsyncEnumerable<TMessage> GetStreamInternalAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _sessionCts.Token);
        var token = streamCts.Token;

        Task<bool>? pendingAlarmTask = null;
        Task<bool>? pendingTelemTask = null;

        try
        {
            while (!token.IsCancellationRequested)
            {
                // 1. 최우선 순위: 대기 중인 모든 알람 즉시 배출 (알람 절대 무유실 및 선순위 보장)
                while (_alarmStream.Reader.TryRead(out var alarm))
                {
                    yield return alarm;
                }

                // 2. 텔레메트리가 있으면 배출 후 다시 알람 검사로 복귀
                if (_telemetryStream.Reader.TryRead(out var telemetry))
                {
                    yield return telemetry;
                    continue;
                }

                // 3. 채널이 예외로 비정상 종료되었으면 해당 예외를 표면화(Surface)하여 재전달
                if (_alarmStream.Reader.Completion.IsFaulted)
                {
                    await _alarmStream.Reader.Completion.ConfigureAwait(false);
                }
                if (_telemetryStream.Reader.Completion.IsFaulted)
                {
                    await _telemetryStream.Reader.Completion.ConfigureAwait(false);
                }

                // 양쪽 채널이 정상 완료되었으면 스트림 종료
                if (_alarmStream.Reader.Completion.IsCompleted && _telemetryStream.Reader.Completion.IsCompleted)
                {
                    break;
                }

                token.ThrowIfCancellationRequested();

                // 4. 대기 작업 단일 슬롯 재사용: 기존 태스크가 완료되었거나 비어있을 때만 새로 생성
                if (pendingAlarmTask == null || pendingAlarmTask.IsCompleted)
                {
                    var vt = _alarmStream.Reader.WaitToReadAsync(token);
                    if (vt.IsCompletedSuccessfully && vt.Result)
                    {
                        if (_alarmStream.Reader.TryRead(out var a))
                            yield return a;
                        pendingAlarmTask = null;
                        continue;
                    }
                    pendingAlarmTask = vt.AsTask();
                }

                if (pendingTelemTask == null || pendingTelemTask.IsCompleted)
                {
                    var vt = _telemetryStream.Reader.WaitToReadAsync(token);
                    if (vt.IsCompletedSuccessfully && vt.Result)
                    {
                        if (_telemetryStream.Reader.TryRead(out var t))
                            yield return t;
                        pendingTelemTask = null;
                        continue;
                    }
                    pendingTelemTask = vt.AsTask();
                }

                // Task.WhenAny 대기 (미완료된 쪽의 태스크는 pending에 남아 다음 루프에서 그대로 재사용됨)
                var completedTask = await Task.WhenAny(pendingAlarmTask, pendingTelemTask).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                if (completedTask.IsFaulted)
                {
                    await completedTask.ConfigureAwait(false);
                }

                // 대기 해제 후 즉시 읽기
                bool gotAlarm = _alarmStream.Reader.TryRead(out var pAlarm);
                if (gotAlarm)
                {
                    yield return pAlarm!;
                }

                bool gotTelem = _telemetryStream.Reader.TryRead(out var pTelem);
                if (gotTelem)
                {
                    yield return pTelem!;
                }

                if (!gotAlarm && !gotTelem)
                {
                    if (_alarmStream.Reader.Completion.IsFaulted)
                        await _alarmStream.Reader.Completion.ConfigureAwait(false);
                    if (_telemetryStream.Reader.Completion.IsFaulted)
                        await _telemetryStream.Reader.Completion.ConfigureAwait(false);
                    if (_alarmStream.Reader.Completion.IsCompleted && _telemetryStream.Reader.Completion.IsCompleted)
                        break;
                }
            }
        }
        finally
        {
            // 스트림 종료(소비자 조기 break 또는 예외) 시 전용 CTS를 Cancel하여 채널 대기자 즉시 취소
            try
            {
                streamCts.Cancel();
            }
            catch (ObjectDisposedException) { }

            // 남은 대기 Task 예외 관찰 및 완료 대기
            if (pendingAlarmTask != null && !pendingAlarmTask.IsCompleted)
            {
                try { await pendingAlarmTask.ConfigureAwait(false); } catch { }
            }
            if (pendingTelemTask != null && !pendingTelemTask.IsCompleted)
            {
                try { await pendingTelemTask.ConfigureAwait(false); } catch { }
            }

            streamCts.Dispose();
        }

        token.ThrowIfCancellationRequested();
    }

    public IAsyncEnumerable<TMessage> Stream => GetStreamAsync();

    public async IAsyncEnumerable<TMessage> GetAlarmsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _sessionCts.Token);
        var token = linkedCts.Token;

        while (await _alarmStream.Reader.WaitToReadAsync(token).ConfigureAwait(false))
        {
            while (_alarmStream.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }

    private static bool DefaultIsAlarm(TMessage message)
    {
        if (message == null) return false;
        var text = message.ToString();
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("ALARM", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("FAULT", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("EMERGENCY", StringComparison.OrdinalIgnoreCase);
    }

    private async ValueTask EnqueueIncomingMessageAsync(TMessage message)
    {
        bool isAlarm = _sessionOptions.IsAlarmMessage != null
            ? _sessionOptions.IsAlarmMessage(message)
            : DefaultIsAlarm(message);

        if (isAlarm)
        {
            // 알람 메시지: 텔레메트리 큐와 물리적으로 완전 격리된 알람 전용 채널에 기록
            if (_alarmStream.Writer.TryWrite(message))
            {
                _observer?.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                    "STREAM", ReadOnlyMemory<byte>.Empty, message?.ToString(), TimeSpan.Zero));
                return;
            }

            // 알람 큐 포화 시 처리 (공통 디스패치 루프 블로킹 방지 및 조용한 유실 원천 차단)
            await HandleAlarmQueueOverflowAsync(message).ConfigureAwait(false);
            return;
        }

        // 일반 텔레메트리 메시지: 텔레메트리 전용 버퍼 만료 시 정책에 따라 처리 및 유실 건수 관측
        if (!_telemetryStream.Writer.TryWrite(message))
        {
            if (_sessionOptions.TelemetryOverflowMode == BoundedChannelFullMode.DropOldest)
            {
                if (_telemetryStream.Reader.TryRead(out var droppedOld))
                {
                    long dropped = Interlocked.Increment(ref _droppedTelemetryCount);
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Rx, TrafficKind.PeriodicTelemetry,
                        "TELEMETRY_DROPPED", ReadOnlyMemory<byte>.Empty,
                        $"Dropped 1 telemetry message ({droppedOld}) due to slow consumer. Total dropped: {dropped}",
                        TimeSpan.Zero, LogLevel.Warning));
                }
                _telemetryStream.Writer.TryWrite(message);
            }
            else if (_sessionOptions.TelemetryOverflowMode == BoundedChannelFullMode.DropWrite)
            {
                long dropped = Interlocked.Increment(ref _droppedTelemetryCount);
                _observer?.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Rx, TrafficKind.PeriodicTelemetry,
                    "TELEMETRY_DROPPED_LATEST", ReadOnlyMemory<byte>.Empty,
                    $"Dropped latest telemetry message due to queue full. Total dropped: {dropped}",
                    TimeSpan.Zero, LogLevel.Warning));
                return;
            }
            else
            {
                // BoundedChannelFullMode.Wait (Backpressure: 소비자가 읽을 때까지 안전하게 대기)
                try
                {
                    await _telemetryStream.Writer.WriteAsync(message, _sessionCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        _observer?.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow, PacketDirection.Rx, TrafficKind.PeriodicTelemetry,
            "STREAM", ReadOnlyMemory<byte>.Empty, message?.ToString(), TimeSpan.Zero));
    }

    private async ValueTask HandleAlarmQueueOverflowAsync(TMessage message)
    {
        switch (_sessionOptions.AlarmOverflowMode)
        {
            case AlarmOverflowMode.SpoolToStorage:
                if (_alarmSpoolQueue != null)
                {
                    if (_alarmSpoolQueue.Writer.TryWrite(message))
                    {
                        _observer?.OnPacketTrace(new PacketTraceRecord(
                            DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                            "ALARM_ENQUEUED_TO_SPOOL", ReadOnlyMemory<byte>.Empty,
                            $"Enqueued overflowed alarm ({message}) to background spool queue.",
                            TimeSpan.Zero, LogLevel.Warning));
                        return;
                    }

                    // 스풀 큐마저 포화되면 명시적으로 세션 중단 (데이터 무유실 및 페일패스트 계약)
                    var spoolOverflowEx = new AlarmBufferOverflowException(
                        $"Alarm spool queue capacity ({_sessionOptions.AlarmSpoolQueueCapacity}) saturated. Aborting session to prevent data loss.");
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                        "ALARM_SPOOL_OVERFLOW_FAILFAST", ReadOnlyMemory<byte>.Empty, spoolOverflowEx.Message, TimeSpan.Zero, LogLevel.Critical));

                    _ = Task.Run(() => PerformCleanupAsync(spoolOverflowEx));
                    throw spoolOverflowEx;
                }
                goto default;

            case AlarmOverflowMode.DropOldestWithWarning:
                if (_alarmStream.Reader.TryRead(out var droppedOldAlarm))
                {
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                        "ALARM_DROPPED_WARNING", ReadOnlyMemory<byte>.Empty,
                        $"CRITICAL: Dropped oldest alarm ({droppedOldAlarm}) due to alarm queue saturation.",
                        TimeSpan.Zero, LogLevel.Critical));
                }
                _alarmStream.Writer.TryWrite(message);
                return;

            case AlarmOverflowMode.ThrowAndAbort:
            default:
                var ex = new AlarmBufferOverflowException(
                    $"Alarm queue capacity ({_sessionOptions.AlarmQueueCapacity}) saturated by slow consumer. Aborting session to prevent silent alarm loss.");
                _observer?.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                    "ALARM_BUFFER_OVERFLOW_FAILFAST", ReadOnlyMemory<byte>.Empty, ex.Message, TimeSpan.Zero, LogLevel.Critical));
                
                // 비동기로 세션 페일패스트 정리 수행 (디스패치 루프 블로킹 방지)
                _ = Task.Run(() => PerformCleanupAsync(ex));
                throw ex;
        }
    }
}
