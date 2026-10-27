namespace Kable.SharedMemory.Tests.Transports;

using Xunit;

public class InboundCopyTests
{
    [Theory]
    [InlineData(1, 256, 1)]
    [InlineData(64, 256, 1)]
    [InlineData(4095, 256, 79)]
    [InlineData(4096, 8192, 4096)]
    [InlineData(4097, 8192, 4097)]
    [InlineData(65536, 256, 997)]
    [InlineData(65536, 65536, 65536)]
    [InlineData(1048576, 1024, 8191)]
    public async Task Transfer_PreservesEveryByteAcrossChunkAndRingBoundaries(
        int length, int capacity, int chunkSize)
    {
        string name = "copy_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, capacity);
        await using var client = SharedMemoryTransport.ConnectClientEndpoint(name);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        byte[] payload = TransportTestSupport.Pattern(length);

        Task<byte[]> receive = TransportTestSupport.ReceiveAsync(server.Input, length, timeout.Token);
        await TransportTestSupport.SendAsync(client.Output, payload, chunkSize, timeout.Token);

        Assert.Equal(payload, await receive);
    }

    [Fact]
    public async Task FullDuplex_WrappedStreamsRemainIndependent()
    {
        string name = "duplex_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, 1024);
        await using var client = SharedMemoryTransport.ConnectClientEndpoint(name);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        byte[] upstream = TransportTestSupport.Pattern(262144, 3);
        byte[] downstream = TransportTestSupport.Pattern(262177, 97);
        Task<byte[]> atServer = TransportTestSupport.ReceiveAsync(server.Input, upstream.Length, timeout.Token);
        Task<byte[]> atClient = TransportTestSupport.ReceiveAsync(client.Input, downstream.Length, timeout.Token);

        await Task.WhenAll(
            TransportTestSupport.SendAsync(client.Output, upstream, 511, timeout.Token),
            TransportTestSupport.SendAsync(server.Output, downstream, 4097, timeout.Token));

        Assert.Equal(upstream, await atServer);
        Assert.Equal(downstream, await atClient);
    }

    [Fact]
    public async Task SlowConsumer_BackpressureResumesWithoutLoss()
    {
        string name = "slow_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, 256);
        await using var client = SharedMemoryTransport.ConnectClientEndpoint(name);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] payload = TransportTestSupport.Pattern(1048576);
        Task<byte[]> receive = TransportTestSupport.ReceiveAsync(server.Input, payload.Length, timeout.Token,
            async () => { held.SetResult(); await resume.Task.WaitAsync(timeout.Token); });
        Task send = TransportTestSupport.SendAsync(client.Output, payload, payload.Length, timeout.Token);

        try
        {
            await held.Task.WaitAsync(timeout.Token);
            Assert.False(send.IsCompleted);
        }
        finally { resume.TrySetResult(); }

        await send;
        Assert.Equal(payload, await receive);
    }
}
