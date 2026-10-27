namespace Kable.SharedMemory.Tests.Transports;

using Xunit;

public class TransportLifecycleTests
{
    [Fact]
    public async Task PeerDispose_ClosesInputAndConnectionToken()
    {
        string name = "peer_close_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, 256);
        await using var client = SharedMemoryTransport.ConnectClientEndpoint(name);
        Task closed = Task.Delay(Timeout.Infinite, client.ConnectionClosed);
        await server.DisposeAsync();
        var result = await client.Input.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(result.IsCompleted);
        client.Input.AdvanceTo(result.Buffer.End);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closed.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbortOrDispose_UnblocksSaturatedOutputAndAllDisposers(bool abort)
    {
        string name = "saturated_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, 256);
        await using var client = SharedMemoryTransport.ConnectClientEndpoint(name);
        byte[] payload = TransportTestSupport.Pattern(1048576);
        var send = client.Output.WriteAsync(payload).AsTask();
        var first = await server.Input.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(send.IsCompleted);
        if (abort) client.Abort("test cancellation under pressure");
        Task[] disposal = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () => await client.DisposeAsync())).ToArray();
        await Task.WhenAll(disposal).WaitAsync(TimeSpan.FromSeconds(5));
        await send.WaitAsync(TimeSpan.FromSeconds(5));
        server.Input.AdvanceTo(first.Buffer.End);
    }

    [Fact]
    public async Task ManyIdleEndpoints_CanAllResumeAndDispose()
    {
        var endpoints = new List<SharedMemoryConnectionContext>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            for (int i = 0; i < 64; i++)
            {
                string name = "idle_" + Guid.NewGuid().ToString("N");
                endpoints.Add(SharedMemoryTransport.CreateServerEndpoint(name, 256));
                endpoints.Add(SharedMemoryTransport.ConnectClientEndpoint(name));
            }
            Task[] transfers = Enumerable.Range(0, 64).Select(async i =>
            {
                var server = endpoints[2 * i];
                var client = endpoints[2 * i + 1];
                Task<byte[]> receive = TransportTestSupport.ReceiveAsync(server.Input, 1, timeout.Token);
                await client.Output.WriteAsync(new byte[] { (byte)i }, timeout.Token);
                Assert.Equal(new byte[] { (byte)i }, await receive);
            }).ToArray();
            await Task.WhenAll(transfers);
        }
        finally
        {
            await Task.WhenAll(endpoints.Select(async endpoint => await endpoint.DisposeAsync()))
                .WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
