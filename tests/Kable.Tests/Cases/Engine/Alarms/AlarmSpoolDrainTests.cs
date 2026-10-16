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
public sealed class AlarmSpoolDrainTests
{
    [Fact(Timeout = 5000)]
    public async Task TC_REL_26_AlarmSpool_OverallDrainTimeout_CompletesPromptlyWithResidualAccounting()
    {
        // 다수의 대기 알람이 있더라도 전체 단일 배출 제한시간(AlarmSpoolDrainTimeout) 내에 워커가 종료되고 미처리 건수가 집계되는지 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();

        var options = new KableSessionOptions<string>
        {
            AlarmQueueCapacity = 1,
            AlarmSpoolQueueCapacity = 50,
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            AlarmSpoolTimeout = TimeSpan.FromSeconds(5),
            AlarmSpoolDrainTimeout = TimeSpan.FromMilliseconds(200), // 전체 배출에 단 200ms만 부여
            UnspooledAlarmDrainPolicy = UnspooledAlarmDrainPolicy.DrainWithinTimeout,
            IsAlarmMessage = msg => msg.StartsWith("$ALARM", StringComparison.OrdinalIgnoreCase),
            OnAlarmOverflowAsync = async (msg, ct) =>
            {
                // 메시지당 50ms 지연 (30개면 순차 실행 시 1,500ms 이상 소요)
                await Task.Delay(50, ct);
                return true;
            }
        };

        var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        // 알람 30건 주입 (1건 스트림 + 29건 스풀 큐)
        for (int i = 1; i <= 30; i++)
        {
            await factory.Context.WriteAsciiLineAsync($"$ALARM_{i:D2}", 0x0A);
        }

        // 약간의 시간 후 세션 종료 (워커가 일부만 저장하고 배출 타임아웃에 도달하도록)
        await Task.Delay(60);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var disposeTask = session.DisposeAsync().AsTask();
        var completed = await Task.WhenAny(disposeTask, Task.Delay(2000));
        stopwatch.Stop();

        completed.Should().Be(disposeTask, "DisposeAsync must complete promptly within overall drain timeout");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1500, "Worker termination must adhere to overall drain timeout");

        var summary = session.SpoolSummary;
        // 일부는 저장되고, 나머지는 배출 시간 초과로 Unprocessed 또는 InDoubt로 완벽히 집계되어야 함
        (summary.StoredCount + summary.InDoubtCount + summary.UnprocessedCount).Should().Be(29);
        summary.UnprocessedCount.Should().BeGreaterThan(0, "Remaining alarms after overall drain timeout must be tracked as Unprocessed");
    }

    [Fact(Timeout = 5000)]
    public async Task TC_REL_28_AlarmSpool_DrainFailureAndExceptions_TrackedAsUnprocessed()
    {
        // 종료 배출 중 실패(false) 또는 예외 발생 시 조용히 묻히지 않고 Unprocessed로 정확히 집계되는지 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        var blockFirstTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new KableSessionOptions<string>
        {
            AlarmQueueCapacity = 1,
            AlarmSpoolQueueCapacity = 10,
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            AlarmSpoolDrainTimeout = TimeSpan.FromSeconds(2),
            UnspooledAlarmDrainPolicy = UnspooledAlarmDrainPolicy.DrainWithinTimeout,
            IsAlarmMessage = msg => msg.StartsWith("$ALARM", StringComparison.OrdinalIgnoreCase),
            OnAlarmOverflowAsync = async (msg, ct) =>
            {
                if (msg.Contains("FAIL"))
                {
                    return false; // 저장 실패
                }
                if (msg.Contains("THROW"))
                {
                    throw new System.IO.IOException("Storage disk full");
                }
                await Task.Yield();
                return true;
            }
        };

        var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        // 큐에 정상 1개, 실패 1개, 예외 1개 주입
        await factory.Context.WriteAsciiLineAsync("$ALARM_01", 0x0A); // 스트림으로
        await factory.Context.WriteAsciiLineAsync("$ALARM_02_OK", 0x0A);
        await factory.Context.WriteAsciiLineAsync("$ALARM_03_FAIL", 0x0A);
        await factory.Context.WriteAsciiLineAsync("$ALARM_04_THROW", 0x0A);
        await Task.Delay(50);

        Func<Task> disposeAct = async () => await session.DisposeAsync();
        await disposeAct.Should().ThrowAsync<AlarmBufferOverflowException>();

        var summary = session.SpoolSummary;
        summary.UnprocessedCount.Should().Be(2, "Failed and thrown alarms during spooling must be tracked as Unprocessed");
        summary.StoredCount.Should().Be(1, "Successfully stored alarm must be tracked as Stored");
    }

    [Fact(Timeout = 8000)]
    public async Task TC_REL_29_AlarmSpool_DrainTimeoutDecoupledFromFixed2sCleanup_AwaitsWorkerToCompletion()
    {
        // 배출 제한시간은 워커가 관리하고(3초), Dispose는 기존 고정 2초에 끊기지 않고 실제 워커 종료까지 대기함을 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();

        var options = new KableSessionOptions<string>
        {
            AlarmQueueCapacity = 1,
            AlarmSpoolQueueCapacity = 20,
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            AlarmSpoolTimeout = TimeSpan.FromSeconds(5),
            AlarmSpoolDrainTimeout = TimeSpan.FromSeconds(3), // 3초 배출 시간 설정
            UnspooledAlarmDrainPolicy = UnspooledAlarmDrainPolicy.DrainWithinTimeout,
            IsAlarmMessage = msg => msg.StartsWith("$ALARM", StringComparison.OrdinalIgnoreCase),
            OnAlarmOverflowAsync = async (msg, ct) =>
            {
                // 메시지당 250ms 소요 -> 12개면 약 3,000ms 소요
                await Task.Delay(250, ct);
                return true;
            }
        };

        var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        // 13개 알람 주입 (1개 스트림 + 12개 스풀 큐)
        for (int i = 1; i <= 13; i++)
        {
            await factory.Context.WriteAsciiLineAsync($"$ALARM_{i:D2}", 0x0A);
        }

        // 워커가 첫 메시지를 가져갈 때까지 잠시 대기
        await Task.Delay(50);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var disposeTask = session.DisposeAsync().AsTask();

        // 핵심 검증 1: 기존 2.0초 고정 정리 타임아웃 시점(2.1초)에 Dispose가 먼저 완료되지 않아야 함!
        await Task.Delay(2100);
        disposeTask.IsCompleted.Should().BeFalse("Dispose must NOT prematurely complete at fixed 2s timeout when drain timeout is 3s");

        // 핵심 검증 2: 3초 배출 완료 시점 이후에는 Dispose가 정상 완료되어야 함
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(3));
        stopwatch.Stop();

        disposeTask.IsCompleted.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(2500, "Dispose must wait for the 3s worker drain to finish");

        // 핵심 검증 3: Dispose 반환 시점에 모든 알람(12개)의 결과가 Stored + InDoubt + Unprocessed로 정확히 집계됨
        var summary = session.SpoolSummary;
        (summary.StoredCount + summary.InDoubtCount + summary.UnprocessedCount).Should().Be(12);
        summary.StoredCount.Should().BeGreaterThanOrEqualTo(8, "At least 8 messages should be stored in ~2.5-3.0s");
    }
}
