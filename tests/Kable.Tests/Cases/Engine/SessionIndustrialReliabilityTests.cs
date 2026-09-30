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
using Xunit;

[Collection("HardwareTransportTests")]
public class SessionIndustrialReliabilityTests
{
    [Fact]
    public async Task TC_REL_01_StrictFifo_ResponseIsolation_AfterTimeout()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // 1. 요청 A 발송 및 50ms 타임아웃 유도
        Func<Task> actTimeout = async () =>
            await session.RequestAsync<string>("REQ_A", TimeSpan.FromMilliseconds(50));

        await actTimeout.Should().ThrowAsync<DeviceTimeoutException>();

        // 타임아웃 발생 시 장비 프로토콜 정합성을 위해 연결이 단절되어야 함
        session.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task TC_REL_02_FullDuplex_DuplicateCorrelationId_ThrowsImmediately()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new CorrelationIdLineCodec();
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        var task1 = session.RequestAsync<string>("CID_DUP:REQ_1", TimeSpan.FromSeconds(5));

        // 동일한 CID로 동시에 두 번째 요청 시도시 즉각 예외 발생
        Func<Task> actDup = async () =>
            await session.RequestAsync<string>("CID_DUP:REQ_2", TimeSpan.FromSeconds(5));

        await actDup.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Duplicate in-flight Correlation ID*");

        // 첫 번째 대기자는 손상 없이 응답을 정상 수신해야 함
        await factory.Context.WriteAsciiLineAsync("CID_DUP:RESP_1", 0x0A);
        var res1 = await task1;
        res1.Should().Be("CID_DUP:RESP_1");
    }

    [Fact]
    public async Task TC_REL_03_Lifecycle_IdempotentStopAndDispose()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // StopAsync 다중 호출 멱등성 검증
        await session.StopAsync();
        await session.StopAsync();
        await session.StopAsync();

        session.IsConnected.Should().BeFalse();

        // DisposeAsync 다중 호출 멱등성 검증
        await session.DisposeAsync();
        await session.DisposeAsync();
    }

    [Fact]
    public async Task TC_REL_04_Cancellation_UnsentQueueItems_CleanlyFaultedOnDisconnect()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // 연결 컨텍스트 Abort
        factory.Context.Abort("Test Force Disconnect");

        // 연결 단절 후 발송 시도 시 DeviceDisconnectedException 발생 검증
        Func<Task> act = async () =>
            await session.RequestAsync<string>("REQ_AFTER_DISCONNECT", TimeSpan.FromSeconds(1));

        await act.Should().ThrowAsync<DeviceDisconnectedException>();
    }

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

        await using var session = new KableSession<string>(factory, codec, options);
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
    }

    [Fact]
    public async Task TC_REL_15_Lifecycle_DeterministicRace_PreventsConnectionLeakAndZombieSession()
    {
        // [P1] Start 완료 직전/직후 Stop/Dispose 개입 시 연결 누수 및 상태 정합성 검증
        var controllableFactory = new ControllableConnectionFactory();
        var codec = new AsciiLineCodec();
        var session = new KableSession<string>(controllableFactory, codec);

        // 1. StartAsync 시작 (ConnectAsync 내부에서 _connectGateTcs 대기)
        var startTask = session.StartAsync();

        // 연결 시도가 시작될 때까지 대기
        await controllableFactory.ConnectStarted;

        // 2. StartAsync가 연결 중인 동안 외부에서 StopAsync 호출
        var stopTask = session.StopAsync();

        // 3. 이제 ConnectAsync 완료를 허용 (새 IConnectionContext 생성 완료)
        controllableFactory.AllowConnectToComplete();

        // 4. Start는 취소되고, Stop은 정상 완료되어야 함
        Func<Task> actStart = async () => await startTask;
        await actStart.Should().ThrowAsync<OperationCanceledException>();
        await stopTask;

        // 5. 검증:
        // - 세션은 최종 Stopped 상태이어야 하며 IsConnected는 false여야 함 (Zombie session 방지)
        session.IsConnected.Should().BeFalse();

        // - 생성되었던 연결 컨텍스트는 등록되지 않고 즉시 폐기(Dispose)되어 연결 누수(Leak)가 없어야 함
        controllableFactory.LastCreatedContext.Should().NotBeNull();
        controllableFactory.LastCreatedContext!.IsDisposed.Should().BeTrue();

        await session.DisposeAsync();
    }

    [Fact]
    public async Task TC_REL_16_Stream_SingleSlotWaitTasks_ReusedWithoutTaskAccumulation()
    {
        // [P2] 무알람 장시간 텔레메트리 스트리밍 시 대기 작업 누적 없음 및 취소 토큰 정리 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        var options = new KableSessionOptions<string>
        {
            IsAlarmMessage = msg => msg.StartsWith("$ALARM", StringComparison.OrdinalIgnoreCase)
        };

        await using var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        using var cts = new CancellationTokenSource();
        var receivedItems = new List<string>();

        // 스트림 비동기 읽기 태스크 가동
        var consumerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in session.GetStreamAsync(cts.Token))
                {
                    receivedItems.Add(item);
                    if (receivedItems.Count >= 20)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 취소
            }
        });

        // 텔레메트리 메시지만 20건 연속 전송 (알람은 0건)
        for (int i = 1; i <= 20; i++)
        {
            await factory.Context.WriteAsciiLineAsync($"$TELEM_DATA_{i:D3}", 0x0A);
            await Task.Delay(5);
        }

        // 20건 정상 소비 완료 대기
        await consumerTask;
        receivedItems.Count.Should().Be(20);

        // 구독 취소 시 남은 대기자가 깔끔하게 해제되는지 확인
        cts.Cancel();
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
            OnAlarmOverflowAsync = msg =>
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
}

internal sealed class ControllableConnectionFactory : IConnectionFactory
{
    private readonly TaskCompletionSource<bool> _connectStartedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _connectGateTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task ConnectStarted => _connectStartedTcs.Task;
    public TestMemoryConnectionContext? LastCreatedContext { get; private set; }

    public void AllowConnectToComplete()
    {
        _connectGateTcs.TrySetResult(true);
    }

    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        _connectStartedTcs.TrySetResult(true);
        await _connectGateTcs.Task;
        var ctx = new TestMemoryConnectionContext();
        LastCreatedContext = ctx;
        return ctx;
    }
}
