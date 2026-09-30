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
    public async Task TC_REL_18_ConnectFailure_StopAndDispose_CompletesWithinTimeout()
    {
        // [1] 연결 실패 후 Stop·Dispose가 무한 대기하지 않고 제한시간 내 즉시 완료되는지 검증
        var failingFactory = new FailingConnectionFactory();
        var codec = new AsciiLineCodec();
        var session = new KableSession<string>(failingFactory, codec);

        // 1. 연결 실패 유도
        Func<Task> actStart = async () => await session.StartAsync();
        await actStart.Should().ThrowAsync<System.Net.Sockets.SocketException>();

        // 2. 연결 실패 후 StopAsync 및 DisposeAsync가 블로킹 없이 즉시 완료되어야 함
        var stopTask = session.StopAsync().AsTask();
        var completedStop = await Task.WhenAny(stopTask, Task.Delay(2000));
        completedStop.Should().Be(stopTask, "StopAsync must complete within timeout even after connect failure");

        var disposeTask = session.DisposeAsync().AsTask();
        var completedDispose = await Task.WhenAny(disposeTask, Task.Delay(2000));
        completedDispose.Should().Be(disposeTask, "DisposeAsync must complete within timeout even after connect failure");
    }

    [Fact(Timeout = 5000)]
    public async Task TC_REL_19_ConcurrentStart_InvokesConnectAsync_ExactlyOnce()
    {
        // [2] 동시 Start 여러 건에도 ConnectAsync() 호출은 정확히 1회만 일어나는지 검증
        var countingFactory = new CountingDelayedConnectionFactory();
        var codec = new AsciiLineCodec();
        await using var session = new KableSession<string>(countingFactory, codec);

        // 10개의 동시 StartAsync 호출
        var startTasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            startTasks.Add(session.StartAsync().AsTask());
        }

        // 연결 시도가 최초 1회 시작될 때까지 대기
        await countingFactory.ConnectStarted;

        // 연결 완료 허용
        countingFactory.AllowConnectToComplete();

        // 모든 10개 호출자가 성공적으로 완료되어야 함
        await Task.WhenAll(startTasks);

        session.IsConnected.Should().BeTrue();

        // 핵심 검증: ConnectAsync는 정확히 1회만 호출되어야 함
        countingFactory.ConnectInvocationCount.Should().Be(1);
    }

    [Fact(Timeout = 10000)]
    public async Task TC_REL_20_Stream_RepeatedEarlyBreak_CleansUpWaitersWithoutAccumulation()
    {
        // [4] await foreach에서 조기 break를 반복해도 대기자가 누적되지 않고 정상 동작하는지 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // 50회 연속으로 스트림을 열고, 메시지 1개 수신 후 즉시 break
        for (int i = 1; i <= 50; i++)
        {
            var writeTask = factory.Context.WriteAsciiLineAsync($"$DATA_{i}", 0x0A);

            await foreach (var item in session.GetStreamAsync())
            {
                item.Should().Be($"$DATA_{i}");
                break; // 조기 break
            }

            await writeTask;
        }

        // 반복적인 break 이후에도 세션 송수신 및 스트림이 완벽하게 정상 동작해야 함
        await factory.Context.WriteAsciiLineAsync("$FINAL_STREAM_DATA", 0x0A);
        await foreach (var item in session.GetStreamAsync())
        {
            item.Should().Be("$FINAL_STREAM_DATA");
            break;
        }

        session.IsConnected.Should().BeTrue();
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

        await using var session = new KableSession<string>(factory, codec, options);
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

        await using var session = new KableSession<string>(factory, codec, options);
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
    }
}

internal sealed class FailingConnectionFactory : IConnectionFactory
{
    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        throw new System.Net.Sockets.SocketException(10061); // Connection Refused
    }
}

internal sealed class CountingDelayedConnectionFactory : IConnectionFactory
{
    private int _connectCount;
    private readonly TaskCompletionSource<bool> _startedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _gateTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int ConnectInvocationCount => Volatile.Read(ref _connectCount);
    public Task ConnectStarted => _startedTcs.Task;

    public void AllowConnectToComplete() => _gateTcs.TrySetResult(true);

    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _connectCount);
        _startedTcs.TrySetResult(true);
        await _gateTcs.Task;
        return new TestMemoryConnectionContext();
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
