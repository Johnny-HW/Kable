namespace Kable.Tests.Cases.Engine.Streaming;

using System.Threading.Channels;
using Kable.Codecs;
using Kable.Engine;
using Kable.Engine.Streaming;
using Kable.Exceptions;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class InboundDeliveryTests
{
    [Theory]
    [InlineData(BoundedChannelFullMode.DropOldest, 2, 3)]
    [InlineData(BoundedChannelFullMode.DropNewest, 1, 3)]
    [InlineData(BoundedChannelFullMode.DropWrite, 1, 2)]
    public void DroppingPoliciesAreAtomicAndCountDrops(BoundedChannelFullMode mode, int first, int second)
    {
        var delivery = new InboundDelivery<int>(2, 1, mode, _ => { });
        delivery.Publish(1); delivery.Publish(2); delivery.Publish(3);
        Assert.Equal(1, delivery.DroppedCount);
        Assert.True(delivery.Stream.Reader.TryRead(out int a)); Assert.Equal(first, a);
        Assert.True(delivery.Stream.Reader.TryRead(out int b)); Assert.Equal(second, b);
    }

    [Fact]
    public async Task WaitBacklogPreservesOrderAndWorkerStopsOnCancellation()
    {
        var delivery = new InboundDelivery<int>(1, 3, BoundedChannelFullMode.Wait, _ => { });
        delivery.Publish(1); delivery.Publish(2); delivery.Publish(3);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var worker = delivery.RunAsync(stop.Token);
        for (int i = 1; i <= 3; i++) Assert.Equal(i, await delivery.Stream.Reader.ReadAsync(stop.Token));
        delivery.Complete(null); stop.Cancel();
        await worker.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(0, delivery.DroppedCount);
    }

    [Fact]
    public async Task SaturationFailsSessionInsteadOfBlockingResponseLoop()
    {
        var link = new DesignLink();
        await using var session = new KableSession<string>(link, new AsciiLineCodec(),
            new KableSessionOptions<string> { InboundQueueCapacity = 1, InboundDeliveryCapacity = 1 });
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await session.StartAsync(stop.Token);
        await link.SendAsync("$D1\n$D2\n$D3\n$D4\n$D5\n", stop.Token);
        while (session.IsConnected) await Task.Delay(5, stop.Token);
        await using var reader = session.Stream.GetAsyncEnumerator(stop.Token);
        await Assert.ThrowsAsync<TelemetryBufferOverflowException>(() => reader.MoveNextAsync().AsTask());
    }
}
