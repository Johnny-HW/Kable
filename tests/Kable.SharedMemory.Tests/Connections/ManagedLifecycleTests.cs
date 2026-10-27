namespace Kable.SharedMemory.Tests.Connections;

using System.Diagnostics;
using Kable.SharedMemory.Connections;
using Xunit;

[CollectionDefinition("SharedMemory resource measurements", DisableParallelization = true)]
public class ManagedResourceCollection;

[Collection("SharedMemory resource measurements")]
public class ManagedLifecycleTests
{
    [Fact]
    public async Task RepeatedReconnects_DoNotAccumulateNativeHandles()
    {
        string name = "cycle_" + Guid.NewGuid().ToString("N");
        await using var listener = new SharedMemoryServerListener(name);
        for (int i = 0; i < 20; i++) await ConnectAndCloseAsync();
        await Task.Delay(100); // Registered-wait/overlapped completion callbacks may finish after the last continuation.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        int before = process.HandleCount;
        int threadsBefore = process.Threads.Count;
        for (int i = 0; i < 100; i++) await ConnectAndCloseAsync();
        await Task.Delay(100);
        process.Refresh();
        int beforeCollection = process.HandleCount;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        process.Refresh();
        int after = process.HandleCount;
        Assert.True(after - before < 16,
            $"Native handles {before} -> {beforeCollection} -> {after} (after finalizers); threads {threadsBefore} -> {process.Threads.Count}.");

        async Task ConnectAndCloseAsync()
        {
            var (server, client) = await ManagedConnectionTests.ConnectAsync(listener, name);
            await client.DisposeAsync();
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task ListenerStopsWhileRequestAndAcceptArePending_OnlyIpcEnds()
    {
        string name = "pending_stop_" + Guid.NewGuid().ToString("N");
        await using var listener = new SharedMemoryServerListener(name, 256);
        var (server, client) = await ManagedConnectionTests.ConnectAsync(listener, name);
        await using var clientScope = client;
        var pendingRead = client.Input.ReadAsync().AsTask();
        Task nextAccept = listener.AcceptAsync().AsTask();
        await listener.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => nextAccept);
        var ended = await pendingRead.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(ended.IsCompleted);
        client.Input.AdvanceTo(ended.Buffer.End);
        Assert.True(server.ConnectionClosed.IsCancellationRequested);
    }
}
