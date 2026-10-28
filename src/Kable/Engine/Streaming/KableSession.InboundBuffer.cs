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
    private Channel<TMessage> _telemetryStream => _delivery.Stream;

    public long DroppedTelemetryCount => _delivery.DroppedCount;

    private bool DefaultIsAlarm(TMessage message)
    {
        return _codec is Kable.Codecs.IAlarmMessageClassifier<TMessage> classifier && classifier.IsAlarmMessage(message);
    }

    private async ValueTask EnqueueIncomingMessageAsync(TMessage message, bool isAlarm)
    {
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

        _delivery.Publish(message);
        _observer?.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow, PacketDirection.Rx, TrafficKind.PeriodicTelemetry,
            "STREAM", ReadOnlyMemory<byte>.Empty, message?.ToString(), TimeSpan.Zero));
    }

    private async ValueTask HandleAlarmQueueOverflowAsync(TMessage message)
    {
        switch (_sessionOptions.AlarmOverflowMode)
        {
            case AlarmOverflowMode.SpoolToStorage:
                if (_sessionOptions.AlarmOverflowMode == AlarmOverflowMode.SpoolToStorage)
                {
                    if (_alarmSpool.TryEnqueue(message))
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
