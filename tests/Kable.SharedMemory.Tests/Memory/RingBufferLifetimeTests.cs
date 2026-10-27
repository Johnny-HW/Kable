namespace Kable.SharedMemory.Tests.Memory;

using Kable.SharedMemory.Memory;
using Xunit;

public class RingBufferLifetimeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(3)]
    public void InvalidCapacity_IsRejectedBeforeMapping(int capacity)
        => Assert.Throws<ArgumentException>(() => SharedMemoryRingBuffer.Create(
            "invalid_" + Guid.NewGuid().ToString("N"), capacity));

    [Fact]
    public async Task DisposedEndpoint_AllQueriesAndRepeatedClosureAreSafe()
    {
        using var pair = new RingBufferPair();
        pair.Reader.Dispose();
        pair.Reader.Dispose();
        pair.Reader.Close();
        Assert.True(pair.Reader.IsClosed);
        Assert.Equal(0, pair.Reader.Read(new byte[1]));
        Assert.Equal(0, pair.Reader.Write(new byte[1]));
        Assert.True(pair.Reader.WaitForData(0));
        Assert.True(pair.Reader.WaitForSpace(0));
        Assert.True(await pair.Reader.WaitForDataAsync());
        Assert.True(await pair.Reader.WaitForSpaceAsync());
        Assert.True(pair.Writer.IsClosed);
    }

    [Fact]
    public async Task WaitsInOppositeDirections_OnSameEndpointAreIndependent()
    {
        using var pair = new RingBufferPair();
        Task<bool> data = pair.Writer.WaitForDataAsync().AsTask();
        Assert.True(await pair.Writer.WaitForSpaceAsync());
        pair.Reader.Close();
        Assert.True(await data.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousWait_StillSupportsCancellationAndReuse(bool space)
    {
        using var pair = new RingBufferPair();
        pair.Prepare(space);
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> wait = Task.Run(() =>
        {
            started.SetResult();
            return space ? pair.Writer.WaitForSpace(Timeout.Infinite, cancellation.Token)
                : pair.Reader.WaitForData(Timeout.Infinite, cancellation.Token);
        });
        await started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => wait.WaitAsync(TimeSpan.FromSeconds(5)));
        pair.Signal(space);
        Assert.True(await pair.WaitAsync(space));
    }

    [Fact]
    public async Task ReadyAsyncWaits_HaveNoManagedAllocationAfterWarmup()
    {
        using var pair = new RingBufferPair();
        pair.Signal(false);
        for (int i = 0; i < 100; i++) await pair.Reader.WaitForDataAsync();
        long before = GC.GetAllocatedBytesForCurrentThread();
        int ready = 0;
        for (int i = 0; i < 10000; i++)
            if (await pair.Reader.WaitForDataAsync()) ready++;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(10000, ready);
        Assert.Equal(0, allocated);
    }
}
