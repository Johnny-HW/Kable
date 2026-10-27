namespace Kable.SharedMemory.Tests.Memory;

using Xunit;

public class RingBufferWaitTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingWait_IsNonBlockingAndResumesWhenConditionBecomesReady(bool space)
    {
        using var pair = new RingBufferPair();
        pair.Prepare(space);
        Task<bool> wait = pair.WaitAsync(space).AsTask();
        Assert.False(wait.IsCompleted);
        pair.Signal(space);
        Assert.True(await wait.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadyCondition_CompletesSynchronously(bool space)
    {
        using var pair = new RingBufferPair();
        if (!space) pair.Signal(false);
        ValueTask<bool> wait = pair.WaitAsync(space);
        Assert.True(wait.IsCompletedSuccessfully);
        Assert.True(await wait);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 25)]
    [InlineData(true, 25)]
    public async Task UnreadyCondition_TimesOut(bool space, int timeout)
    {
        using var pair = new RingBufferPair();
        pair.Prepare(space);
        Assert.False(await pair.WaitAsync(space, timeout).AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleSignal_DoesNotReportReadiness(bool space)
    {
        using var pair = new RingBufferPair();
        pair.Prepare(space);
        pair.Signal(space);
        if (space) pair.Writer.Write(new byte[] { 7 });
        else pair.Reader.Read(new byte[1]);
        Assert.False(await pair.WaitAsync(space, 25).AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidTimeout_IsRejected(bool space)
    {
        using var pair = new RingBufferPair();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pair.WaitAsync(space, -2).AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCanceledToken_IsPreservedEvenWhenReady(bool space)
    {
        using var pair = new RingBufferPair();
        if (!space) pair.Signal(false);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pair.WaitAsync(space, ct: cancellation.Token).AsTask());
        Assert.Equal(cancellation.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelPendingWait_DoesNotConsumeNextPublication(bool space)
    {
        using var pair = new RingBufferPair();
        pair.Prepare(space);
        using var cancellation = new CancellationTokenSource();
        Task<bool> wait = pair.WaitAsync(space, ct: cancellation.Token).AsTask();
        cancellation.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => wait.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Task<bool> next = pair.WaitAsync(space).AsTask();
        Assert.False(next.IsCompleted);
        pair.Signal(space);
        Assert.True(await next.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentWaitOnSameDirection_IsRejected(bool space)
    {
        using var pair = new RingBufferPair();
        pair.Prepare(space);
        Task<bool> first = pair.WaitAsync(space).AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => pair.WaitAsync(space).AsTask());
        pair.ClosePeer(space);
        Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PeerCloseOrLocalDispose_WakesPendingAndFutureWaits(bool space, bool localDispose)
    {
        using var pair = new RingBufferPair();
        pair.Prepare(space);
        Task<bool> wait = pair.WaitAsync(space).AsTask();
        if (localDispose) pair.DisposeWaiter(space);
        else pair.ClosePeer(space);
        Assert.True(await wait.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(await pair.WaitAsync(space));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutThenNewWait_DoesNotLeaveAnEventConsumer(bool space)
    {
        using var pair = new RingBufferPair();
        pair.Prepare(space);
        Assert.False(await pair.WaitAsync(space, 10));
        Task<bool> next = pair.WaitAsync(space).AsTask();
        pair.Signal(space);
        Assert.True(await next.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
