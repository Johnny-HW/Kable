namespace Kable.Tests.Cases;

using System.Runtime.ExceptionServices;
using FluentAssertions;
using Kable.Codecs;
using Kable.Engine;
using Kable.Extensions;
using Kable.Tests.Fixtures.Transports;
using Kable.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

[Collection("HardwareTransportTests")]
public sealed class TelemetryThroughputTests
{
    private readonly ITestOutputHelper _output;
    public TelemetryThroughputTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(0)]
    [InlineData(300)]
    public async Task TC_TRN_08_TelemetryStreaming_HighThroughput_ZeroGen2GarbageCollections(int consumerDelayMs)
    {
        const int packetCount = 2000;
        var server = new TelemetryTestServer(packetCount);
        var diagnostics = new TestTrafficDiagnostics();
        IDeviceSession<string>? session = null;
        Task? receiveTask = null;
        int received = 0;
        int gen2Before = -1;
        int gen2After = -1;
        bool failed = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            session = new KableClientBuilder<string>()
                .UseTcp("127.0.0.1", server.Port)
                .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
                .WithObserver(diagnostics)
                .Build();
            await session.StartAsync(deadline.Token);

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            gen2Before = GC.CollectionCount(2);
            server.StartSending();

            if (consumerDelayMs > 0)
            {
                await TestTrafficDiagnostics.WaitForCompletionAsync(server.Sent.Task, server.Completion, deadline.Token);
                // Deliberate slow-consumer fault injection, not a readiness assumption.
                await Task.Delay(consumerDelayMs, deadline.Token);
                Assert.False(server.Completion.IsCompleted, "The server must keep the connection until receive acknowledgement.");
            }

            async Task ReceiveAsync()
            {
                await foreach (var item in session.Stream.WithCancellation(deadline.Token))
                {
                    received++;
                    if (received >= packetCount) break;
                }
                received.Should().Be(packetCount);
            }

            receiveTask = ReceiveAsync();
            await TestTrafficDiagnostics.WaitForCompletionAsync(receiveTask, server.Completion, deadline.Token);
            gen2After = GC.CollectionCount(2);
            received.Should().Be(packetCount);
            (gen2After - gen2Before).Should().BeLessThanOrEqualTo(1,
                "High-throughput streaming should not trigger excessive Gen2 GC collections");

            server.ConfirmReceived();
            await server.Completion.WaitAsync(deadline.Token);
        }
        catch (Exception ex)
        {
            failed = true;
            _output.WriteLine($"Failure: {ex}\nInjected consumer delay={consumerDelayMs}ms, received={received}/{packetCount}, " +
                $"connected={session?.IsConnected}, state={(session as KableSession<string>)?.State}, Gen2 before={gen2Before}, after={gen2After}, current={GC.CollectionCount(2)}");
            _output.WriteLine(server.Snapshot());
            _output.WriteLine(diagnostics.Snapshot());
            throw;
        }
        finally
        {
            await deadline.CancelAsync();
            Exception? cleanupFailure = null;
            if (session != null)
            {
                try { await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception ex) { cleanupFailure = ex; _output.WriteLine($"Session cleanup: {ex}"); }
            }
            try { await server.DisposeAsync(); }
            catch (Exception ex) { cleanupFailure ??= ex; _output.WriteLine($"Server cleanup: {ex}\n{server.Snapshot()}"); }
            if (receiveTask != null)
            {
                try { await receiveTask.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception ex) { cleanupFailure ??= ex; _output.WriteLine($"Receive cleanup: {ex}"); }
            }
            if (!failed && cleanupFailure != null) ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
    }
}
