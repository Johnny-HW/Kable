using System.Diagnostics;
namespace Kable.Comparison.Faults;

public static class ResourceProbes
{
    public static async Task<List<ResourceRow>> RunAsync(string name, string mode, int cycles)
    {
        for (int i = 0; i < 10; i++) await CycleAsync(i);
        var rows = new List<ResourceRow> { await SnapshotAsync(name, mode, 0) };
        int checkpoint = Math.Max(1, cycles / 4);
        for (int i = 1; i <= cycles; i++)
        {
            await CycleAsync(i);
            if (i % checkpoint == 0 || i == cycles) rows.Add(await SnapshotAsync(name, mode, i));
        }
        return rows;
        async Task CycleAsync(int index)
        {
            var result = await FaultProbes.RunAsync(name, mode, "cancel-inflight", index);
            if (!result.Passed) throw new InvalidOperationException($"Resource cycle failed: {result}");
        }
    }
    private static async Task<ResourceRow> SnapshotAsync(string name, string mode, int cycles)
    {
        await Task.Delay(100);
        // Diagnostic full collection only BETWEEN batches, never in latency measurements.
        long managed = GC.GetTotalMemory(true);
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return new(name, mode, cycles, managed, process.PrivateMemorySize64,
            process.HandleCount, process.Threads.Count);
    }
}
