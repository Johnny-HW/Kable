namespace Kable.Tests.Cases.Profiles;

using System.Runtime.ExceptionServices;
using FluentAssertions;
using Kable.Engine.Profiles;
using Kable.Tests.Fixtures.Profiles;
using Kable.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

public partial class KableProfileClientTests
{
    private readonly ITestOutputHelper _output;

    public KableProfileClientTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(0)]
    [InlineData(300)]
    public async Task ProfileClient_GenericEnum_PeriodicPollingAndLatestCache_WorksCorrectly(int firstResponseDelayMs)
    {
        var server = new ProfilePollingServer(firstResponseDelayMs);
        var diagnostics = new TestTrafficDiagnostics();
        IKableProfileClient<TestPeriodicCmd, TestAperiodicCmd>? client = null;
        var statusReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pressureReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnected = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool failed = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        void OnPeriodic(TestPeriodicCmd command, string value)
        {
            if (command == TestPeriodicCmd.GetStatus) statusReceived.TrySetResult(value);
            if (command == TestPeriodicCmd.GetPressure) pressureReceived.TrySetResult(value);
        }
        void OnDisconnected(Exception? reason) => disconnected.TrySetResult(reason);

        try
        {
            var config = new KableProfileConfig<TestPeriodicCmd, TestAperiodicCmd>()
                .UseTcp("127.0.0.1", server.Port)
                .WithTimeout(TimeSpan.FromSeconds(2))
                .AddPeriodic(TestPeriodicCmd.GetStatus, "?STATUS", TimeSpan.FromMilliseconds(50))
                .AddPeriodic(TestPeriodicCmd.GetPressure, "?PRESSURE", TimeSpan.FromMilliseconds(50))
                .AddAperiodic(TestAperiodicCmd.InitRobot, "CMD:INIT");

            client = await KableProfileManager.ConnectAsync(config, diagnostics, deadline.Token);
            client.PeriodicDataReceived += OnPeriodic;
            client.Disconnected += OnDisconnected;
            client.IsConnected.Should().BeTrue();

            await TestTrafficDiagnostics.WaitForCompletionAsync(server.FirstRequest.Task, server.Completion, deadline.Token, disconnected.Task);
            Assert.False(statusReceived.Task.IsCompleted);
            Assert.False(pressureReceived.Task.IsCompleted);
            server.AllowResponses();
            await TestTrafficDiagnostics.WaitForCompletionAsync(
                Task.WhenAll(statusReceived.Task, pressureReceived.Task), server.Completion, deadline.Token, disconnected.Task);

            client.GetLatest(TestPeriodicCmd.GetStatus).Should().Be("STATUS:READY");
            client.GetLatest(TestPeriodicCmd.GetPressure).Should().Be("PRESSURE:101.3");
            (await statusReceived.Task).Should().Be("STATUS:READY");
            (await pressureReceived.Task).Should().Be("PRESSURE:101.3");

            var (latestVal, updatedTime) = client.GetLatestWithTimestamp(TestPeriodicCmd.GetStatus);
            latestVal.Should().Be("STATUS:READY");
            updatedTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
            client.TryGetFresh(TestPeriodicCmd.GetStatus, TimeSpan.FromSeconds(5), out var freshVal).Should().BeTrue();
            freshVal.Should().Be("STATUS:READY");

            var init = client.ExecuteAsync(TestAperiodicCmd.InitRobot, ct: deadline.Token).AsTask();
            await TestTrafficDiagnostics.WaitForCompletionAsync(init, server.Completion, deadline.Token, disconnected.Task);
            (await init).Should().Be("OK:INIT");
        }
        catch (Exception ex)
        {
            failed = true;
            _output.WriteLine($"Failure: {ex}\nInjected delay={firstResponseDelayMs}ms, connected={client?.IsConnected}, " +
                $"status={client?.GetLatest(TestPeriodicCmd.GetStatus)}, pressure={client?.GetLatest(TestPeriodicCmd.GetPressure)}, " +
                $"statusEvent={statusReceived.Task.Status}, pressureEvent={pressureReceived.Task.Status}, disconnect={disconnected.Task.Status}");
            if (disconnected.Task.IsCompletedSuccessfully) _output.WriteLine($"Disconnect reason: {await disconnected.Task}");
            _output.WriteLine(server.Snapshot());
            _output.WriteLine(diagnostics.Snapshot());
            throw;
        }
        finally
        {
            await deadline.CancelAsync();
            Exception? cleanupFailure = null;
            if (client != null)
            {
                client.PeriodicDataReceived -= OnPeriodic;
                client.Disconnected -= OnDisconnected;
                try { await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception ex) { cleanupFailure = ex; _output.WriteLine($"Client cleanup: {ex}"); }
            }
            try { await server.DisposeAsync(); }
            catch (Exception ex) { cleanupFailure ??= ex; _output.WriteLine($"Server cleanup: {ex}\n{server.Snapshot()}"); }
            if (!failed && cleanupFailure != null) ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
    }
}
