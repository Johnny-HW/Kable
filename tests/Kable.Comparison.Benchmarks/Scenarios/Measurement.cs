using System.Diagnostics;
using Kable.Comparison.Adapters;
using Kable.Comparison.Infrastructure;
namespace Kable.Comparison.Scenarios;

public sealed record Measurement(string Library, int Round, int Bytes, int Connections,
    int Operations, double ConnectBatchMs, double MeanUs, double P50Us, double P95Us,
    double P99Us, double RequestsPerSecond, double AllocatedBytesPerOperation,
    double CpuMs, int Gen0, int Gen1, int Gen2);

public static class Measure
{
    public static async Task<Measurement> RunAsync(string name, int round, int bytes,
        int connections, int iterations, int warmup)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = deadline.Token;
        await using var server = new EchoServer();
        var clients = new List<IComparisonClient>();
        var connect = Stopwatch.StartNew();
        try
        {
            for (int i = 0; i < connections; i++)
            {
                var client = Clients.Create(name);
                clients.Add(client);
                await client.ConnectAsync(server.Port, ct);
            }
            connect.Stop();
            string request = new('Q', bytes);
            await Task.WhenAll(clients.Select(async client =>
            {
                for (int i = 0; i < warmup; i++)
                    Validate(request, await client.QueryAsync(request, ct));
            }));
            int perClient = Math.Max(1, (iterations + connections - 1) / connections);
            var samples = clients.Select(_ => new double[perClient]).ToArray();
            using var process = Process.GetCurrentProcess();
            var cpu = process.TotalProcessorTime;
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            long allocated = GC.GetTotalAllocatedBytes(true);
            var clock = Stopwatch.StartNew();
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workers = clients.Select((client, index) => WorkAsync(client, samples[index], start.Task)).ToArray();
            start.SetResult();
            await Task.WhenAll(workers);
            clock.Stop();
            long allocationDelta = GC.GetTotalAllocatedBytes(true) - allocated;
            double cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
            int d0 = GC.CollectionCount(0) - g0, d1 = GC.CollectionCount(1) - g1, d2 = GC.CollectionCount(2) - g2;
            var sorted = samples.SelectMany(s => s).Order().ToArray();
            double Percentile(double p) => sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * p) - 1)];
            return new(name, round, bytes, connections, sorted.Length, connect.Elapsed.TotalMilliseconds,
                sorted.Average(), Percentile(.5), Percentile(.95), Percentile(.99),
                sorted.Length / clock.Elapsed.TotalSeconds, (double)allocationDelta / sorted.Length,
                cpuMs, d0, d1, d2);

            async Task WorkAsync(IComparisonClient client, double[] target, Task gate)
            {
                await gate;
                for (int i = 0; i < target.Length; i++)
                {
                    long before = Stopwatch.GetTimestamp();
                    string response = await client.QueryAsync(request, ct);
                    target[i] = Stopwatch.GetElapsedTime(before).TotalMicroseconds;
                    Validate(request, response);
                }
            }
        }
        finally
        {
            // Dispose every client even if one implementation fails during shutdown.
            var cleanup = await Task.WhenAll(clients.Select(async client =>
            {
                try { await client.DisposeAsync(); return (Exception?)null; }
                catch (Exception error) { return error; }
            }));
            var failures = cleanup.OfType<Exception>().ToArray();
            if (failures.Length > 0) throw new AggregateException(failures);
        }
    }
    private static void Validate(string expected, string actual)
    {
        if (actual != expected) throw new InvalidDataException("Response mismatch; measurement rejected.");
    }
}
