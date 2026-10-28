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

    private bool DefaultIsAlarm(TMessage message)
    {
        return _codec is Kable.Codecs.IAlarmMessageClassifier<TMessage> classifier && classifier.IsAlarmMessage(message);
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
