namespace Kable.Benchmarks;

using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Core.Metrics;
using Kable.Engine;
using Kable.Transports;

/// <summary>
/// Profiles round-trip latency statistics (P50, P95, P99, CV) using ZeroAllocLatencyCollector.
/// </summary>
public static class LatencyProfileRunner
{
    public static async Task<LatencySummary> RunSessionLatencyProfileAsync(int iterations = 10_000)
    {
        var (client, server) = InMemoryConnectionContext.CreatePair();
        using var stopCts = new CancellationTokenSource();

        var echoTask = Task.Run(async () =>
        {
            var codec = new AsciiLineCodec();
            byte[] reply = Encoding.ASCII.GetBytes("PONG\n");
            try
            {
                while (!stopCts.IsCancellationRequested)
                {
                    var result = await server.Input.ReadAsync(stopCts.Token);
                    var buffer = result.Buffer;
                    while (codec.TryDecode(ref buffer, out _))
                    {
                        await server.Output.WriteAsync(reply, stopCts.Token);
                    }
                    server.Input.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted) break;
                }
            }
            catch (OperationCanceledException) { }
        });

        await using var session = new KableSession<string>(new MockFactory(client), new AsciiLineCodec());
        await session.StartAsync();

        // 1. Warm-up JIT and pipelines
        for (int i = 0; i < 200; i++)
        {
            await session.RequestAsync<string>("PING", TimeSpan.FromSeconds(2));
        }

        // 2. Measure with ZeroAllocLatencyCollector
        var collector = new ZeroAllocLatencyCollector(iterations);

        for (int i = 0; i < iterations; i++)
        {
            long start = Stopwatch.GetTimestamp();
            await session.RequestAsync<string>("PING", TimeSpan.FromSeconds(2));
            collector.RecordDuration(start);
        }

        stopCts.Cancel();
        try { await echoTask; } catch { }

        await session.DisposeAsync();
        await server.DisposeAsync();

        return collector.ComputeSummary();
    }

    private sealed class MockFactory(IConnectionContext context) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(context);
    }
}
