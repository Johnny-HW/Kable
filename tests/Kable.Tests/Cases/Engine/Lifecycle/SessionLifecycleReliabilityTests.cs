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
public sealed class SessionLifecycleReliabilityTests
{
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

    [Fact(Timeout = 5000)]
    public async Task TC_REL_30_Lifecycle_CleanupFailure_PropagatesToAllCallersAndDisposesResourceOnce()
    {
        // 워커 종료 예외를 주입한 뒤 최초 Stop, 동시 Stop, 후속 Stop, Dispose가 모두 같은 실패를 전달하고,
        // 연결·자원 폐기는 정확히 한 번 수행되는지 검증
        var trackingFactory = new TrackingDisposableConnectionFactory();
        var codec = new AsciiLineCodec();

        var options = new KableSessionOptions<string>
        {
            AlarmQueueCapacity = 1,
            AlarmSpoolQueueCapacity = 10,
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            AlarmSpoolDrainTimeout = TimeSpan.FromSeconds(1),
            UnspooledAlarmDrainPolicy = UnspooledAlarmDrainPolicy.DrainWithinTimeout,
            IsAlarmMessage = msg => msg.StartsWith("$ALARM", StringComparison.OrdinalIgnoreCase),
            OnAlarmOverflowAsync = (msg, ct) =>
            {
                // 종료 배출 시 의도적인 워커 예외 발생
                throw new InvalidOperationException("Injected Worker Terminal Fault");
            }
        };

        var session = new KableSession<string>(trackingFactory, codec, options);
        await session.StartAsync();

        // 알람 주입하여 스풀 워커가 배출 중 예외를 던지도록 구성
        await trackingFactory.LastCreatedContext!.WriteAsciiLineAsync("$ALARM_01", 0x0A);
        await trackingFactory.LastCreatedContext!.WriteAsciiLineAsync("$ALARM_02_SPOOL", 0x0A);
        await Task.Delay(50);

        // 1. 최초 Stop 호출과 동시 Stop 호출을 동시에 발송
        var stopTask1 = session.StopAsync().AsTask();
        var stopTask2 = session.StopAsync().AsTask();

        // 두 호출 모두 동일한 실패를 전달받아야 함
        Func<Task> act1 = async () => await stopTask1;
        Func<Task> act2 = async () => await stopTask2;
        (await act1.Should().ThrowAsync<Exception>()).WithInnerException<InvalidOperationException>().WithMessage("*Injected Worker Terminal Fault*");
        (await act2.Should().ThrowAsync<Exception>()).WithInnerException<InvalidOperationException>().WithMessage("*Injected Worker Terminal Fault*");

        // 2. 이미 종료된 상태에서의 후속 Stop 호출도 동일한 실패를 전달받아야 함
        Func<Task> act3 = async () => await session.StopAsync();
        (await act3.Should().ThrowAsync<Exception>()).WithInnerException<InvalidOperationException>().WithMessage("*Injected Worker Terminal Fault*");

        // 3. 후속 Dispose 호출 역시 동일한 실패를 전달받으면서 자원을 안전하게 폐기해야 함
        Func<Task> act4 = async () => await session.DisposeAsync();
        (await act4.Should().ThrowAsync<Exception>()).WithInnerException<InvalidOperationException>().WithMessage("*Injected Worker Terminal Fault*");

        // 4. 핵심 검증: StopAsync가 실패해도 연결 리소스 폐기(DisposeAsync)는 정확히 1회만 수행되어야 함!
        trackingFactory.LastCreatedContext.DisposeCallCount.Should().Be(1, "Connection context must be disposed exactly once");
        session.State.Should().Be(SessionLifecycleState.Disposed);
    }
}
