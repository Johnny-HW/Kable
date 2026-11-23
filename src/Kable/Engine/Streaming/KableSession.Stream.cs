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
                bool alarmCompleted = _alarmStream.Reader.Completion.IsCompleted;
                bool telemCompleted = _telemetryStream.Reader.Completion.IsCompleted;
                if (alarmCompleted && telemCompleted)
                {
                    break;
                }

                token.ThrowIfCancellationRequested();

                // 4. 대기 작업 단일 슬롯 재사용: 열린 채널만 대기하여 단일 채널 완료 시 CPU 스핀 방지
                if (alarmCompleted)
                {
                    pendingAlarmTask = null;
                }
                else if (pendingAlarmTask == null || pendingAlarmTask.IsCompleted)
                {
                    var vt = _alarmStream.Reader.WaitToReadAsync(token);
                    if (vt.IsCompletedSuccessfully)
                    {
                        if (vt.Result)
                        {
                            if (_alarmStream.Reader.TryRead(out var a))
                                yield return a;
                            pendingAlarmTask = null;
                            continue;
                        }
                        alarmCompleted = true;
                        pendingAlarmTask = null;
                    }
                    else
                    {
                        pendingAlarmTask = vt.AsTask();
                    }
                }

                if (telemCompleted)
                {
                    pendingTelemTask = null;
                }
                else if (pendingTelemTask == null || pendingTelemTask.IsCompleted)
                {
                    var vt = _telemetryStream.Reader.WaitToReadAsync(token);
                    if (vt.IsCompletedSuccessfully)
                    {
                        if (vt.Result)
                        {
                            if (_telemetryStream.Reader.TryRead(out var t))
                                yield return t;
                            pendingTelemTask = null;
                            continue;
                        }
                        telemCompleted = true;
                        pendingTelemTask = null;
                    }
                    else
                    {
                        pendingTelemTask = vt.AsTask();
                    }
                }

                if (alarmCompleted && telemCompleted)
                {
                    break;
                }

                Task completedTask;
                if (pendingAlarmTask != null && pendingTelemTask != null)
                {
                    completedTask = await Task.WhenAny(pendingAlarmTask, pendingTelemTask).ConfigureAwait(false);
                }
                else
                {
                    completedTask = (pendingAlarmTask ?? pendingTelemTask)!;
                    await completedTask.ConfigureAwait(false);
                }
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
            if (pendingAlarmTask != null)
            {
                if (!pendingAlarmTask.IsCompleted)
                {
                    try
                    {
                        await pendingAlarmTask.ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[KableSession.Stream] Pending alarm task cancelled/faulted: {ex.Message}");
                    }
                }
                else if (pendingAlarmTask.IsFaulted)
                {
                    _ = pendingAlarmTask.Exception;
                }
            }
            if (pendingTelemTask != null)
            {
                if (!pendingTelemTask.IsCompleted)
                {
                    try
                    {
                        await pendingTelemTask.ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[KableSession.Stream] Pending telemetry task cancelled/faulted: {ex.Message}");
                    }
                }
                else if (pendingTelemTask.IsFaulted)
                {
                    _ = pendingTelemTask.Exception;
                }
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

}
