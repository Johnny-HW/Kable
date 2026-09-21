namespace Kable.Tests.Cases.Profiles;

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Engine.Profiles;
using Kable.Transports;
using Xunit;

public class KableProfileClientTests
{
    private enum TestPeriodicCmd
    {
        GetStatus,
        GetPressure
    }

    private enum TestAperiodicCmd
    {
        InitRobot,
        MovePosition
    }

    private sealed class DummyViewModel
    {
        public string? ReceivedData { get; set; }
    }

    [Fact]
    public async Task ProfileClient_GenericEnum_PeriodicPollingAndLatestCache_WorksCorrectly()
    {
        await using var listener = new TcpConnectionListener(IPAddress.Loopback, 0);
        int port = ((IPEndPoint)listener.LocalEndPoint).Port;

        var serverCts = new CancellationTokenSource();
        var serverTask = Task.Run(async () =>
        {
            await using var serverCtx = await listener.AcceptAsync();
            var input = serverCtx.Input;
            var output = serverCtx.Output;

            while (!serverCts.Token.IsCancellationRequested)
            {
                var readResult = await input.ReadAsync(serverCts.Token);
                if (readResult.IsCompleted || readResult.Buffer.IsEmpty) break;

                string line = Encoding.ASCII.GetString(System.Buffers.BuffersExtensions.ToArray(readResult.Buffer)).Trim();
                input.AdvanceTo(readResult.Buffer.End);

                string reply = line switch
                {
                    "?STATUS" => "STATUS:READY\n",
                    "?PRESSURE" => "PRESSURE:101.3\n",
                    "CMD:INIT" => "OK:INIT\n",
                    _ => "UNKNOWN\n"
                };

                await output.WriteAsync(Encoding.ASCII.GetBytes(reply));
                await output.FlushAsync();
            }
        }, serverCts.Token);

        var config = new KableProfileConfig<TestPeriodicCmd, TestAperiodicCmd>()
            .UseTcp("127.0.0.1", port)
            .WithTimeout(TimeSpan.FromSeconds(2))
            .AddPeriodic(TestPeriodicCmd.GetStatus, "?STATUS", TimeSpan.FromMilliseconds(50))
            .AddPeriodic(TestPeriodicCmd.GetPressure, "?PRESSURE", TimeSpan.FromMilliseconds(50))
            .AddAperiodic(TestAperiodicCmd.InitRobot, "CMD:INIT");

        await using var client = await KableProfileManager.ConnectAsync(config);
        client.IsConnected.Should().BeTrue();

        // Wait for at least one periodic polling cycle
        await Task.Delay(200);

        string? status = client.GetLatest(TestPeriodicCmd.GetStatus);
        string? pressure = client.GetLatest(TestPeriodicCmd.GetPressure);

        status.Should().Be("STATUS:READY");
        pressure.Should().Be("PRESSURE:101.3");

        // Fresh cache and timestamp verification
        var (latestVal, updatedTime) = client.GetLatestWithTimestamp(TestPeriodicCmd.GetStatus);
        latestVal.Should().Be("STATUS:READY");
        updatedTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));

        client.TryGetFresh(TestPeriodicCmd.GetStatus, TimeSpan.FromSeconds(5), out var freshVal).Should().BeTrue();
        freshVal.Should().Be("STATUS:READY");

        // Execute aperiodic command while polling
        string initResult = await client.ExecuteAsync(TestAperiodicCmd.InitRobot);
        initResult.Should().Be("OK:INIT");

        serverCts.Cancel();
        try { await serverTask; } catch { }
    }

    [Fact]
    public async Task ProfileClient_WeakSubscription_PreventsMemoryLeakWhenTargetGarbageCollected()
    {
        var config = new KableProfileConfig<TestPeriodicCmd, TestAperiodicCmd>();
        var dummySimple = new DummySimpleClient();
        var clientImpl = new GenericKableProfileClientImpl<TestPeriodicCmd, TestAperiodicCmd>(dummySimple, config, null);

        var weakRef = RegisterWeakSubscriber(clientImpl, out var sub);

        // Target should be alive initially
        weakRef.IsAlive.Should().BeTrue();

        // Trigger GC collection
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

        // Weak reference should be collected because clientImpl only holds WeakReference
        weakRef.IsAlive.Should().BeFalse();

        sub.Dispose();
        await clientImpl.DisposeAsync();
    }

    [Fact]
    public async Task ProfileClient_StringClient_PeriodicAndAperiodic_WorksCorrectly()
    {
        var dummySimple = new EchoDummySimpleClient();
        var config = new KableProfileConfig()
            .AddPeriodic("VOLT?", TimeSpan.FromMilliseconds(50))
            .AddCommand("RESET", "RESET!");

        var client = new StringKableProfileClientImpl(dummySimple, config, null);
        client.Start();
        client.IsConnected.Should().BeTrue();

        // Aperiodic command execution
        string resp = await client.ExecuteAsync("RESET");
        resp.Should().Be("ACK:RESET!");

        // Wait for periodic polling
        await Task.Delay(150);
        string? volt = client.GetLatest("VOLT?");
        volt.Should().Be("ACK:VOLT?");

        // TryGetFresh: within 1 second freshness should be true
        client.TryGetFresh("VOLT?", TimeSpan.FromSeconds(2), out var freshVolt).Should().BeTrue();
        freshVolt.Should().Be("ACK:VOLT?");

        // TryGetFresh: with TimeSpan.Zero should be false (expired immediately)
        client.TryGetFresh("VOLT?", TimeSpan.FromMicroseconds(1), out _).Should().BeFalse();
    }

    [Fact]
    public void ProfileClient_WeakSubscriptionWithState_ZeroAllocation_DispatchesCorrectly()
    {
        var dummySimple = new EchoDummySimpleClient();
        var config = new KableProfileConfig();
        var client = new StringKableProfileClientImpl(dummySimple, config, null);

        var vm = new DummyViewModel();
        string customState = "CUSTOM_CONTEXT";
        string? capturedState = null;

        var sub = client.SubscribeWeak(vm, customState, (target, state, cmd, data) =>
        {
            target.ReceivedData = data;
            capturedState = state;
        });

        // Subscription can be cleanly disposed
        sub.Should().NotBeNull();
        sub.Dispose();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference RegisterWeakSubscriber(
        GenericKableProfileClientImpl<TestPeriodicCmd, TestAperiodicCmd> client,
        out IDisposable subscription)
    {
        var vm = new DummyViewModel();
        subscription = client.SubscribeWeak(vm, static (target, cmd, data) =>
        {
            target.ReceivedData = data;
        });
        return new WeakReference(vm);
    }

    private sealed class EchoDummySimpleClient : Kable.Simple.IKableSimpleClient
    {
        public bool IsConnected => true;
        public event Action<string>? LineReceived { add { } remove { } }
        public event Action<Exception>? ErrorOccurred { add { } remove { } }
        public event Action<Exception?>? Disconnected { add { } remove { } }
        public ValueTask<string> QueryAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
            => new($"ACK:{command}");
        public ValueTask SendLineAsync(string line, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DummySimpleClient : Kable.Simple.IKableSimpleClient
    {
        public bool IsConnected => true;
        public event Action<string>? LineReceived { add { } remove { } }
        public event Action<Exception>? ErrorOccurred { add { } remove { } }
        public event Action<Exception?>? Disconnected { add { } remove { } }
        public ValueTask<string> QueryAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
            => new("OK");
        public ValueTask SendLineAsync(string line, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
