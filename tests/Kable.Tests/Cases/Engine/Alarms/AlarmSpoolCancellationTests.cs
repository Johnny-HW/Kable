namespace Kable.Tests.Cases;

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Tests.Fixtures;
using Kable.Tests.Fixtures.Codecs;
using Kable.Tests.Fixtures.Engine;
using Xunit;

[Collection("HardwareTransportTests")]
public sealed class AlarmSpoolCancellationTests
{
    [Fact(Timeout = 5000)]
    public async Task TC_REL_24_AlarmSpool_HungCallback_CancelsWorkerAndDisposesWithoutLeakingWorker()
    {
        // 저장 콜백이 영원히 멈추더라도 CancellationToken 취소로 워커가 안전 종료되고 Dispose가 즉시 완료
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        var workerEnteredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new KableSessionOptions<string>
        {
            AlarmQueueCapacity = 1,
            AlarmSpoolQueueCapacity = 5,
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            AlarmSpoolTimeout = TimeSpan.FromSeconds(5),
            IsAlarmMessage = msg => msg.StartsWith("$ALARM", StringComparison.OrdinalIgnoreCase),
            OnAlarmOverflowAsync = async (msg, ct) =>
            {
                workerEnteredTcs.TrySetResult(true);
                // 외부 저장소가 무기한 멈추어 있는 상황 모의 (토큰 취소 대기)
                await Task.Delay(Timeout.Infinite, ct);
                return true;
            }
        };

        var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        // 1. 알람 큐(1개) 채우고 2번째 알람으로 스풀 워커 진입 유도
        await factory.Context.WriteAsciiLineAsync("$ALARM_01", 0x0A);
        await factory.Context.WriteAsciiLineAsync("$ALARM_02_SPOOL", 0x0A);

        // 워커가 저장 콜백 내부로 진입할 때까지 대기
        await workerEnteredTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // 2. 저장 대기를 수동으로 풀지 않은 상태에서 즉시 세션 DisposeAsync 호출!
        // 세션 취소 토큰이 전달되므로 워커가 즉시 취소되고 Dispose가 지체 없이 완료되어야 함
        var disposeTask = session.DisposeAsync().AsTask();
        var completed = await Task.WhenAny(disposeTask, Task.Delay(2000));
        completed.Should().Be(disposeTask, "DisposeAsync must complete promptly even if storage callback was hung");

        session.State.Should().Be(SessionLifecycleState.Disposed);
    }

    [Fact(Timeout = 5000)]
    public async Task TC_REL_25_AlarmSpool_StorageTimeout_AbortsSessionExplicitly()
    {
        // 개별 저장 제한시간(AlarmSpoolTimeout) 초과 시 워커가 세션을 페일패스트 중단하는지 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        var workerStartedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new KableSessionOptions<string>
        {
            AlarmQueueCapacity = 1,
            AlarmSpoolQueueCapacity = 5,
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            AlarmSpoolTimeout = TimeSpan.FromMilliseconds(200), // 짧은 제한시간 설정
            IsAlarmMessage = msg => msg.StartsWith("$ALARM", StringComparison.OrdinalIgnoreCase),
            OnAlarmOverflowAsync = async (msg, ct) =>
            {
                workerStartedTcs.TrySetResult(true);
                // 5초 대기 (200ms 타임아웃에 의해 취소 유도)
                await Task.Delay(5000, ct);
                return true;
            }
        };

        var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        // 알람 주입
        await factory.Context.WriteAsciiLineAsync("$ALARM_01", 0x0A);
        await factory.Context.WriteAsciiLineAsync("$ALARM_02_SPOOL", 0x0A);

        await workerStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // 타임아웃(200ms) 만료 후 세션 자동 페일패스트 중단 대기
        var timeoutLimit = DateTime.UtcNow.AddSeconds(2);
        while (session.IsConnected && DateTime.UtcNow < timeoutLimit)
        {
            await Task.Delay(20);
        }

        session.IsConnected.Should().BeFalse("Session must be aborted due to storage timeout");

        // 페일패스트로 중단된 세션의 DisposeAsync()는 정리 예외를 삼키지 않고 그대로 전파
        Func<Task> disposeAct = async () => await session.DisposeAsync();
        await disposeAct.Should().ThrowAsync<AlarmBufferOverflowException>();
    }

    [Fact(Timeout = 5000)]
    public async Task TC_REL_27_AlarmSpool_InFlightCancellation_TrackedAsInDoubt()
    {
        // 저장 중 세션 취소된 메시지가 '저장 여부 불명(InDoubt)'으로 정확히 기록되는지 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        var workerEnteredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new KableSessionOptions<string>
        {
            AlarmQueueCapacity = 1,
            AlarmSpoolQueueCapacity = 5,
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            IsAlarmMessage = msg => msg.StartsWith("$ALARM", StringComparison.OrdinalIgnoreCase),
            OnAlarmOverflowAsync = async (msg, ct) =>
            {
                workerEnteredTcs.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, ct);
                return true;
            }
        };

        var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        await factory.Context.WriteAsciiLineAsync("$ALARM_01", 0x0A);
        await factory.Context.WriteAsciiLineAsync("$ALARM_02_INFLIGHT", 0x0A);

        await workerEnteredTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // 저장 진행 중에 즉시 세션 종료
        await session.DisposeAsync();

        var summary = session.SpoolSummary;
        summary.InDoubtCount.Should().Be(1, "The message canceled in-flight must be tracked as InDoubt");
        summary.StoredCount.Should().Be(0);
    }
}
