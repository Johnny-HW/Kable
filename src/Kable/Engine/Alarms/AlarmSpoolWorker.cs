namespace Kable.Engine.Alarms;

using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Exceptions;
using Kable.Observability;

internal sealed class AlarmSpoolWorker<TMessage>
{
    private readonly KableSessionOptions<TMessage> _sessionOptions;
    private readonly ICommObserver? _observer;
    private readonly Action<Exception> _onFailure;
    private readonly Channel<TMessage>? _queue;
    private long _spoolStoredCount, _spoolInDoubtCount, _spoolUnprocessedCount;
    internal Task Completion { get; private set; } = Task.CompletedTask;
    internal AlarmSpoolSummary Summary => new(Volatile.Read(ref _spoolStoredCount),
        Volatile.Read(ref _spoolInDoubtCount), Volatile.Read(ref _spoolUnprocessedCount));

    internal AlarmSpoolWorker(KableSessionOptions<TMessage> options, ICommObserver? observer, Action<Exception> onFailure)
    {
        _sessionOptions = options; _observer = observer; _onFailure = onFailure;
        if (options.AlarmOverflowMode != AlarmOverflowMode.SpoolToStorage) return;
        if (options.OnAlarmOverflowAsync == null)
            throw new InvalidOperationException("SpoolToStorage requires OnAlarmOverflowAsync.");
        _queue = Channel.CreateBounded<TMessage>(new BoundedChannelOptions(options.AlarmSpoolQueueCapacity)
        { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, AllowSynchronousContinuations = false });
    }
    internal bool TryEnqueue(TMessage message) => _queue?.Writer.TryWrite(message) == true;
    internal void Complete() => _queue?.Writer.TryComplete();
    internal void Start(CancellationToken token) => Completion = Task.Run(() => RunAsync(token));

    /// <summary>
    /// Background worker loop for persisting overflowed alarms to external storage.
    /// Decouples persistence I/O from the inbound dispatch loop to prevent blocking normal request-response routing.
    /// Passes a CancellationToken with AlarmSpoolTimeout to the callback.
    /// Uses a single overall drain timeout on shutdown and tracks Stored, InDoubt, and Unprocessed alarm counts.
    /// </summary>
    private async Task RunAsync(CancellationToken token)
    {
        if (_queue == null || _sessionOptions.OnAlarmOverflowAsync == null) return;

        var reader = _queue.Reader;

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
                        _onFailure(timeoutEx);
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
                        _onFailure(abortEx);
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

                        _onFailure(abortEx);
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
            _onFailure(ex);
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
            var summary = Summary;
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                "ALARM_SPOOL_DRAIN_SUMMARY", ReadOnlyMemory<byte>.Empty,
                $"Spool worker terminated. Summary: Stored={summary.StoredCount}, InDoubt={summary.InDoubtCount}, Unprocessed={summary.UnprocessedCount}",
                TimeSpan.Zero, summary.UnprocessedCount > 0 || summary.InDoubtCount > 0 ? LogLevel.Warning : LogLevel.Information));
        }
    }
}
