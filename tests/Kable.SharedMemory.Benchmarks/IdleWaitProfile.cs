namespace Kable.SharedMemory.Benchmarks;

using System.Diagnostics;
using System.Text.Json;
using Kable.SharedMemory.Memory;

internal static class IdleWaitProfile
{
    // Fresh process per mode/count. Higher minimum permits all synchronous baseline waiters to start.
    internal static async Task RunAsync(string mode, int count)
    {
        if (mode is not ("sync" or "async") || count <= 0 || count > 512)
            throw new ArgumentException("Expected --idle sync|async 1..512.");
        ThreadPool.GetMinThreads(out int minimum, out int ioMinimum);
        ThreadPool.SetMinThreads(Math.Max(minimum, count + 8), ioMinimum);
        using var stop = new CancellationTokenSource();
        var rings = new List<SharedMemoryRingBuffer>();
        var tasks = new List<Task>();
        try
        {
            ThreadPool.GetAvailableThreads(out int availableBefore, out _);
            var started = new TaskCompletionSource[count];
            for (int i = 0; i < count; i++)
            {
                var ring = SharedMemoryRingBuffer.Create("idle_profile_" + Guid.NewGuid().ToString("N"), 256);
                rings.Add(ring);
                var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                started[i] = signal;
                if (mode == "sync")
                    tasks.Add(Task.Run(() => WaitSynchronously(ring, signal, stop.Token)));
                else
                    tasks.Add(WaitAsynchronously(ring, signal, stop.Token));
            }
            await Task.WhenAll(started.Select(signal => signal.Task)).WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(100);
            using var process = Process.GetCurrentProcess();
            TimeSpan cpuBefore = process.TotalProcessorTime;
            await Task.Delay(500);
            process.Refresh();
            ThreadPool.GetAvailableThreads(out int availableDuring, out _);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Mode = mode, Waiters = count, Pending = tasks.Count(task => !task.IsCompleted),
                WorkerOccupancyDelta = availableBefore - availableDuring,
                PoolThreads = ThreadPool.ThreadCount, ProcessThreads = process.Threads.Count,
                CpuMsOver500Ms = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds,
                MinWorkers = Math.Max(minimum, count + 8)
            }));
        }
        finally
        {
            stop.Cancel();
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
            foreach (var ring in rings) ring.Dispose();
        }
    }

    private static void WaitSynchronously(SharedMemoryRingBuffer ring, TaskCompletionSource started, CancellationToken ct)
    {
        started.SetResult();
        try { ring.WaitForData(Timeout.Infinite, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private static async Task WaitAsynchronously(SharedMemoryRingBuffer ring, TaskCompletionSource started, CancellationToken ct)
    {
        Task<bool> wait = ring.WaitForDataAsync(ct: ct).AsTask();
        started.SetResult();
        try { await wait.ConfigureAwait(false); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}
