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
public sealed class AlarmSpoolRoutingTests
{
    [Fact]
    public async Task TC_REL_14_AlarmQueue_Saturation_ThrowsAndAborts_WithoutBlockingRequestResponse()
    {
        // [P1] 알람 큐 상한 초과 시 Fail-Fast 안전 중단 및 공통 디스패치 루프 비블로킹 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new CorrelationIdLineCodec();
        var options = new KableSessionOptions<string>
        {
            AlarmQueueCapacity = 5,
            AlarmOverflowMode = AlarmOverflowMode.ThrowAndAbort,
            IsAlarmMessage = msg => msg.Contains("ALARM", StringComparison.OrdinalIgnoreCase)
        };

        var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        // 1. 소비자가 알람 스트림을 읽지 않는 상태에서 5개의 알람을 전송하여 큐를 채움
        for (int i = 1; i <= 5; i++)
        {
            await factory.Context.WriteAsciiLineAsync($"$ALARM_LEVEL_{i}", 0x0A);
        }

        // 짧은 대기로 알람 디스패치 반영 보장
        await Task.Delay(50);

        // 2. 알람 큐가 5개 꽉 찬 상태에서도, 동기 요청/응답(CID)은 디스패치 루프가 블로킹되지 않고 정상 라우팅되어야 함
        var reqTask = session.RequestAsync<string>("CID_PING:REQ", TimeSpan.FromSeconds(2));
        await factory.Context.WriteAsciiLineAsync("CID_PING:PONG", 0x0A);
        var resp = await reqTask;
        resp.Should().Be("CID_PING:PONG");

        // 3. 6번째 알람 도착 시 AlarmBufferOverflowException에 의해 세션 페일패스트 중단 트리거
        await factory.Context.WriteAsciiLineAsync("$ALARM_LEVEL_6_OVERFLOW", 0x0A);

        // 세션이 알람 포화로 인해 안전하게 종료되었는지 확인 (최대 2초 폴링 대기)
        var spinTimeout = DateTime.UtcNow.AddSeconds(2);
        while (session.IsConnected && DateTime.UtcNow < spinTimeout)
        {
            await Task.Delay(20);
        }

        session.IsConnected.Should().BeFalse();

        // 페일패스트로 중단된 세션의 DisposeAsync()는 정리 예외를 삼키지 않고 그대로 전파
        Func<Task> disposeAct = async () => await session.DisposeAsync();
        await disposeAct.Should().ThrowAsync<AlarmBufferOverflowException>();
    }

    [Fact]
    public async Task TC_REL_17_AlarmOverflow_SpoolToStorage_ContinuesWithoutAbort()
    {
        // [P1] AlarmOverflowMode.SpoolToStorage 모드 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new CorrelationIdLineCodec();
        var spooledAlarms = new List<string>();

        var options = new KableSessionOptions<string>
        {
            AlarmQueueCapacity = 2,
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            IsAlarmMessage = msg => msg.StartsWith("$ALARM", StringComparison.OrdinalIgnoreCase),
            OnAlarmOverflowAsync = (msg, ct) =>
            {
                spooledAlarms.Add(msg);
                return new ValueTask<bool>(true);
            }
        };

        await using var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        // 알람 2건 전송 -> 알람 채널 꽉 참
        await factory.Context.WriteAsciiLineAsync("$ALARM_01", 0x0A);
        await factory.Context.WriteAsciiLineAsync("$ALARM_02", 0x0A);
        await Task.Delay(20);

        // 3번째, 4번째 알람은 세션 중단 없이 스풀링 콜백으로 넘어가야 함
        await factory.Context.WriteAsciiLineAsync("$ALARM_03", 0x0A);
        await factory.Context.WriteAsciiLineAsync("$ALARM_04", 0x0A);
        await Task.Delay(50);

        session.IsConnected.Should().BeTrue();
        spooledAlarms.Should().Contain(new[] { "$ALARM_03", "$ALARM_04" });
    }

    [Fact(Timeout = 5000)]
    public async Task TC_REL_21_AlarmSpool_SlowStorage_DoesNotBlockRequestResponseRouting()
    {
        // [3] 알람 저장이 느리거나 멈춰도 공통 디스패치는 블로킹되지 않고 요청·응답 라우팅이 정상 유지되는지 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new CorrelationIdLineCodec();
        var tcsHoldStorage = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new KableSessionOptions<string>
        {
            AlarmQueueCapacity = 2,
            AlarmSpoolQueueCapacity = 10,
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            IsAlarmMessage = msg => msg.Contains("ALARM", StringComparison.OrdinalIgnoreCase),
            OnAlarmOverflowAsync = async (msg, ct) =>
            {
                // 저장소 I/O가 무기한 또는 장시간 지연되는 상황 모의
                await tcsHoldStorage.Task;
                return true;
            }
        };

        await using var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        // 1. 알람 큐(2개)를 채우고, 3번째 알람을 보내 스풀 큐로 넘김 (저장소 작업 대기 시작)
        await factory.Context.WriteAsciiLineAsync("$ALARM_01", 0x0A);
        await factory.Context.WriteAsciiLineAsync("$ALARM_02", 0x0A);
        await factory.Context.WriteAsciiLineAsync("$ALARM_OVERFLOW_03", 0x0A);
        await Task.Delay(50);

        // 2. 핵심 검증: 저장소가 멈춰있는 중에도, 동기 요청/응답(CID)은 디스패치 루프가 멈추지 않고 즉시 완료되어야 함!
        var reqTask = session.RequestAsync<string>("CID_PING:REQ", TimeSpan.FromSeconds(2));
        await factory.Context.WriteAsciiLineAsync("CID_PING:PONG", 0x0A);
        var resp = await reqTask;
        resp.Should().Be("CID_PING:PONG");

        // 3. 지연되었던 저장소 완료 허용
        tcsHoldStorage.TrySetResult(true);
    }

    [Fact(Timeout = 5000)]
    public async Task TC_REL_22_AlarmSpool_QueueSaturation_AbortsSessionExplicitly()
    {
        // [3] 저장소가 멈춰서 스풀 큐마저 포화되면 조용히 유실되지 않고 명시적으로 세션을 중단하는지 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new CorrelationIdLineCodec();
        var tcsHoldStorage = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new KableSessionOptions<string>
        {
            AlarmQueueCapacity = 2,
            AlarmSpoolQueueCapacity = 2,
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            IsAlarmMessage = msg => msg.Contains("ALARM", StringComparison.OrdinalIgnoreCase),
            OnAlarmOverflowAsync = async (msg, ct) =>
            {
                await tcsHoldStorage.Task;
                return true;
            }
        };

        var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        // 알람 채널(2개) + 워커 인플라이트(1개) + 스풀 큐 버퍼(2개) = 최대 5개 수용
        // 6번째 이상 주입 시 스풀 큐 포화로 인해 세션 페일패스트 중단 트리거
        for (int i = 1; i <= 7; i++)
        {
            await factory.Context.WriteAsciiLineAsync($"$ALARM_{i:D2}", 0x0A);
        }

        // 세션 안전 중단 대기
        var spinTimeout = DateTime.UtcNow.AddSeconds(3);
        while (session.IsConnected && DateTime.UtcNow < spinTimeout)
        {
            await Task.Delay(20);
        }

        session.IsConnected.Should().BeFalse();
        tcsHoldStorage.TrySetResult(true);

        // 페일패스트로 중단된 세션의 DisposeAsync()는 정리 예외를 삼키지 않고 그대로 전파
        Func<Task> disposeAct = async () => await session.DisposeAsync();
        await disposeAct.Should().ThrowAsync<AlarmBufferOverflowException>();
    }

    [Fact]
    public void TC_REL_23_AlarmSpool_MissingCallback_ThrowsInvalidOperationException()
    {
        // SpoolToStorage 모드인데 콜백이 없으면 설정 오류로 즉시 거부(Fail-Fast)
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        var options = new KableSessionOptions<string>
        {
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            OnAlarmOverflowAsync = null
        };

        var act = () => new KableSession<string>(factory, codec, options);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OnAlarmOverflowAsync*");
    }
}
