namespace Kable.SharedMemory.Tests.Connections;

using Kable.SharedMemory.Connections;
using Kable.SharedMemory.Memory;
using Kable.SharedMemory.Tests.Transports;
using Xunit;

public class ManagedConnectionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(131072)]
    public async Task NewConnection_HasMatchingGenerationAndBidirectionalData(int length)
    {
        string name = "managed_" + Guid.NewGuid().ToString("N");
        await using var listener = new SharedMemoryServerListener(name, 1024);
        var (server, client) = await ConnectAsync(listener, name);
        await using var serverScope = server;
        await using var clientScope = client;
        Assert.NotNull(server.SessionGeneration);
        Assert.Equal(server.SessionGeneration, client.SessionGeneration);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        byte[] outgoing = TransportTestSupport.Pattern(length);
        Task<byte[]> atServer = TransportTestSupport.ReceiveAsync(server.Input, length, timeout.Token);
        await client.Output.WriteAsync(outgoing, timeout.Token);
        Assert.Equal(outgoing, await atServer);
        Task<byte[]> atClient = TransportTestSupport.ReceiveAsync(client.Input, length, timeout.Token);
        await server.Output.WriteAsync(outgoing, timeout.Token);
        Assert.Equal(outgoing, await atClient);
    }

    [Fact]
    public async Task Reconnect_UsesFreshMappingEvenWhenOldMappingIsStillHeld()
    {
        string name = "restart_" + Guid.NewGuid().ToString("N");
        await using var listener = new SharedMemoryServerListener(name, 256);
        var (firstServer, firstClient) = await ConnectAsync(listener, name);
        await using var oldServerScope = firstServer;
        using var oldMapping = SharedMemoryRingBuffer.Open(
            SharedMemoryHandshake.SessionName(name, firstServer.SessionGeneration!.Value) + "_s2c");
        await firstClient.DisposeAsync();
        await WaitClosedAsync(firstServer.ConnectionClosed);
        var (secondServer, secondClient) = await ConnectAsync(listener, name);
        await using var serverScope = secondServer;
        await using var clientScope = secondClient;
        Assert.NotEqual(firstServer.SessionGeneration, secondServer.SessionGeneration);
        Assert.True(oldMapping.IsClosed);
        Assert.Equal(0, oldMapping.Write(new byte[] { 99 }));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await secondServer.Output.WriteAsync(new byte[] { 42 }, timeout.Token);
        Assert.Equal(new byte[] { 42 },
            await TransportTestSupport.ReceiveAsync(secondClient.Input, 1, timeout.Token));
    }

    [Fact]
    public async Task SecondClient_CannotReplaceAnActiveUi()
    {
        string name = "busy_" + Guid.NewGuid().ToString("N");
        await using var listener = new SharedMemoryServerListener(name);
        var (server, client) = await ConnectAsync(listener, name);
        await using var serverScope = server;
        await using var clientScope = client;
        using var cancelAccept = new CancellationTokenSource();
        Task pending = listener.AcceptAsync(cancelAccept.Token).AsTask();
        await Assert.ThrowsAsync<TimeoutException>(() => new SharedMemoryClientFactory(name, 100).ConnectAsync().AsTask());
        Assert.False(client.ConnectionClosed.IsCancellationRequested);
        await client.Output.WriteAsync(new byte[] { 17 });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(new byte[] { 17 }, await TransportTestSupport.ReceiveAsync(server.Input, 1, timeout.Token));
        cancelAccept.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task DuplicateListener_IsRejectedWithoutDisruptingTheFirst()
    {
        string name = "listener_" + Guid.NewGuid().ToString("N");
        await using var listener = new SharedMemoryServerListener(name);
        Assert.Throws<IOException>(() => new SharedMemoryServerListener(name));
        var (server, client) = await ConnectAsync(listener, name);
        await client.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task CancelAccept_ThenAcceptAgainWorks()
    {
        string name = "accept_cancel_" + Guid.NewGuid().ToString("N");
        await using var listener = new SharedMemoryServerListener(name);
        using var cancel = new CancellationTokenSource();
        Task pending = listener.AcceptAsync(cancel.Token).AsTask();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        var (server, client) = await ConnectAsync(listener, name);
        await client.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task DisposeListener_CancelsPendingAcceptAndAllowsNewListener()
    {
        string name = "stop_" + Guid.NewGuid().ToString("N");
        var listener = new SharedMemoryServerListener(name);
        Task pending = listener.AcceptAsync().AsTask();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => await listener.DisposeAsync()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await using var replacement = new SharedMemoryServerListener(name);
    }

    internal static async Task<(SharedMemoryConnectionContext Server, SharedMemoryConnectionContext Client)> ConnectAsync(
        SharedMemoryServerListener listener, string name)
    {
        var accepted = listener.AcceptAsync().AsTask();
        var client = (SharedMemoryConnectionContext)await new SharedMemoryClientFactory(name).ConnectAsync();
        await accepted.WaitAsync(TimeSpan.FromSeconds(5));
        var server = (SharedMemoryConnectionContext)await accepted;
        return (server, client);
    }

    internal static async Task WaitClosedAsync(CancellationToken closed)
        => await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Task.Delay(Timeout.Infinite, closed).WaitAsync(TimeSpan.FromSeconds(5)));
}
