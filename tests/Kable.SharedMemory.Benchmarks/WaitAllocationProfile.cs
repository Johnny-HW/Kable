namespace Kable.SharedMemory.Benchmarks;

using System.Diagnostics;
using System.Text.Json;
using Kable.SharedMemory.Memory;

internal static class WaitAllocationProfile
{
    internal static async Task RunAsync(int iterations)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        string name = "wait_cost_" + Guid.NewGuid().ToString("N");
        using var writer = SharedMemoryRingBuffer.Create(name, 256);
        using var reader = SharedMemoryRingBuffer.Open(name);
        byte[] payload = { 42 };
        byte[] output = new byte[1];
        for (int i = 0; i < 500; i++) await PublishAfterRegistrationAsync();
        writer.Write(payload);
        long beforeReady = GC.GetTotalAllocatedBytes(precise: true);
        for (int i = 0; i < iterations; i++) await reader.WaitForDataAsync();
        long readyBytes = GC.GetTotalAllocatedBytes(precise: true) - beforeReady;
        reader.Read(output);

        var elapsed = new Stopwatch();
        long beforeSlow = GC.GetTotalAllocatedBytes(precise: true);
        elapsed.Start();
        for (int i = 0; i < iterations; i++) await PublishAfterRegistrationAsync();
        elapsed.Stop();
        long slowBytes = GC.GetTotalAllocatedBytes(precise: true) - beforeSlow;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Iterations = iterations, ReadyBytesPerWait = (double)readyBytes / iterations,
            RegisteredBytesPerRoundTrip = (double)slowBytes / iterations,
            RegisteredMeanMicroseconds = elapsed.Elapsed.TotalMicroseconds / iterations,
            Scope = "Process-wide managed allocations including continuations; not end-to-end device latency."
        }));

        async Task PublishAfterRegistrationAsync()
        {
            Task<bool> wait = reader.WaitForDataAsync().AsTask();
            writer.Write(payload);
            if (!await wait.ConfigureAwait(false) || reader.Read(output) != 1 || output[0] != 42)
                throw new InvalidOperationException("Registered wait round-trip lost its publication.");
        }
    }
}
