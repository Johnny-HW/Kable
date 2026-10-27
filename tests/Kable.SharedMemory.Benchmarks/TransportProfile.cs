namespace Kable.SharedMemory.Benchmarks;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime;

internal static partial class TransportProfile
{
    internal static async Task RunAsync(string mode, string scenario, int pairs, int characters, int operations, int seconds,
        bool noGc = false, bool prepareGc = false, int warmupRequests = 100)
    {
        if (mode is not ("raw" or "managed") || scenario is not ("request" or "burst" or "stream") ||
            pairs is < 1 or > 128 || characters < 1 || operations < 0 || seconds < 0 ||
            (operations == 0 && seconds == 0) || (scenario == "stream" && operations == 0))
            throw new ArgumentException("Expected mode scenario pairs characters operations seconds.");
        // Use the same minimum in both builds, so blocked baseline workers do not starve the test driver.
        ThreadPool.GetMinThreads(out int minimumWorkers, out int minimumIo);
        int configuredMinimum = Math.Max(minimumWorkers, pairs * 4 + 16);
        ThreadPool.SetMinThreads(configuredMinimum, minimumIo);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(30, seconds + 15)));
        var links = new List<ProfileLink>();
        bool noGcStarted = false;
        try
        {
            for (int i = 0; i < pairs; i++) links.Add(await ProfileLink.CreateAsync(mode, characters, scenario == "stream", stop.Token));
            if (scenario != "stream")
                foreach (var link in links)
                    for (int i = 0; i < warmupRequests; i++) await link.QueryAsync();
            var histograms = Enumerable.Range(0, pairs).Select(_ => new Histogram()).ToArray();
            if (prepareGc)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            if (noGc)
            {
                noGcStarted = GC.TryStartNoGCRegion(256L * 1024 * 1024, disallowFullBlockingGC: true);
                if (!noGcStarted) throw new InvalidOperationException("Cannot reserve the no-GC measurement budget.");
            }
            using var process = Process.GetCurrentProcess();
            int handlesBefore = process.HandleCount;
            int[] gcBefore = { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
            long waitRegistrationsBefore = ReadWaitCounter("Registrations");
            long waitCallbacksBefore = ReadWaitCounter("Callbacks");
            TimeSpan pauseBefore = GC.GetTotalPauseDuration();
            TimeSpan cpuBefore = process.TotalProcessorTime;
            long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
            long start = Stopwatch.GetTimestamp();
            long deadline = seconds > 0 ? start + (long)seconds * Stopwatch.Frequency : long.MaxValue;
            await Task.WhenAll(links.Select((link, index) => scenario == "stream"
                ? RunStreamAsync(link, operations, histograms[index], stop.Token)
                : RunRequestsAsync(link, histograms[index], operations, deadline, scenario == "burst")));
            double elapsed = Stopwatch.GetElapsedTime(start).TotalSeconds;
            long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
            double pauseMs = (GC.GetTotalPauseDuration() - pauseBefore).TotalMilliseconds;
            double cpuMs = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
            int[] collections = Enumerable.Range(0, 3).Select(generation => GC.CollectionCount(generation) - gcBefore[generation]).ToArray();
            long waitRegistrations = waitRegistrationsBefore < 0 ? -1 : ReadWaitCounter("Registrations") - waitRegistrationsBefore;
            long waitCallbacks = waitCallbacksBefore < 0 ? -1 : ReadWaitCounter("Callbacks") - waitCallbacksBefore;
            var combined = Histogram.Combine(histograms);
            process.Refresh();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Mode = mode, Scenario = scenario, Pairs = pairs, Characters = characters,
                NoGcRequested = noGc, NoGcRegionActive = GCSettings.LatencyMode == GCLatencyMode.NoGCRegion,
                WarmupRequests = warmupRequests,
                Operations = combined.Count, ElapsedSeconds = elapsed,
                WaitRegistrationsPerOperation = waitRegistrations < 0 ? (double?)null : (double)waitRegistrations / combined.Count,
                WaitCallbacksPerOperation = waitCallbacks < 0 ? (double?)null : (double)waitCallbacks / combined.Count,
                OperationsPerSecond = combined.Count / elapsed,
                MeanMicroseconds = scenario == "stream" ? (double?)null : combined.TotalMicroseconds / combined.Count,
                P50Microseconds = scenario == "stream" ? null : combined.Percentile(0.50),
                P95Microseconds = scenario == "stream" ? null : combined.Percentile(0.95),
                P99Microseconds = scenario == "stream" ? null : combined.Percentile(0.99),
                MaxMicroseconds = combined.MaxMicroseconds, combined.Above100Ms,
                AllocatedBytesPerOperation = (double)allocated / combined.Count,
                AllocatedBytesPerSecond = allocated / elapsed, CpuMilliseconds = cpuMs,
                GcCollections = collections, GcPauseMilliseconds = pauseMs,
                WorkerThreads = ThreadPool.ThreadCount, ProcessThreads = process.Threads.Count,
                MinimumWorkerThreads = configuredMinimum,
                HandlesBefore = handlesBefore, HandlesAfter = process.HandleCount,
                EngineSha256 = Hash(typeof(Kable.Engine.KableSession<>).Assembly.Location),
                CoreSha256 = Hash(typeof(Kable.Core.IConnectionContext).Assembly.Location)
            }));
        }
        finally
        {
            if (noGcStarted && GCSettings.LatencyMode == GCLatencyMode.NoGCRegion) GC.EndNoGCRegion();
            stop.Cancel();
            foreach (var link in links) await link.DisposeAsync();
        }
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static long ReadWaitCounter(string field)
        => typeof(SharedMemoryTransport).Assembly.GetType("Kable.SharedMemory.Memory.WaitCostCounters")?
            .GetField(field)?.GetValue(null) is long value ? value : -1;
}
