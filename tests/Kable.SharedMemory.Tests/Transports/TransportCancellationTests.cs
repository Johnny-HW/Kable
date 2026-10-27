namespace Kable.SharedMemory.Tests.Transports;

using System.Text;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Xunit;

public class TransportCancellationTests
{
    [Fact]
    public async Task CancelCallerRead_DoesNotAbortNextReadOrConnection()
    {
        string name = "read_cancel_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, 256);
        await using var client = SharedMemoryTransport.ConnectClientEndpoint(name);
        using var cancellation = new CancellationTokenSource();
        var pending = server.Input.ReadAsync(cancellation.Token).AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(server.ConnectionClosed.IsCancellationRequested);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.Output.WriteAsync(new byte[] { 27 }, timeout.Token);
        Assert.Equal(new byte[] { 27 },
            await TransportTestSupport.ReceiveAsync(server.Input, 1, timeout.Token));
    }

    [Fact]
    public async Task PeerClosesWhileOutputSaturated_LocalPumpExits()
    {
        string name = "peer_full_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, 256);
        await using var client = SharedMemoryTransport.ConnectClientEndpoint(name);
        CancellationToken closed = client.ConnectionClosed;
        var send = client.Output.WriteAsync(TransportTestSupport.Pattern(1048576)).AsTask();
        var first = await server.Input.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(send.IsCompleted);
        await server.DisposeAsync();
        // Pipe flush may complete as the reader advances; it is not a peer acknowledgement.
        await send.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Task.Delay(Timeout.Infinite, closed).WaitAsync(TimeSpan.FromSeconds(5)));
        server.Input.AdvanceTo(first.Buffer.End);
    }

    [Fact]
    public async Task PeerDisconnect_FailsPendingSessionRequestWithoutWaitingForRequestTimeout()
    {
        string name = "session_close_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, 256);
        await using var client = SharedMemoryTransport.ConnectClientEndpoint(name);
        await using var session = new KableSession<string>(new Factory(client), new AsciiLineCodec());
        await session.StartAsync();
        Task<string> request = session.RequestAsync<string>("PING", TimeSpan.FromSeconds(30)).AsTask();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(Encoding.ASCII.GetBytes("PING\n"),
            await TransportTestSupport.ReceiveAsync(server.Input, 5, timeout.Token));
        await server.DisposeAsync();
        await Assert.ThrowsAsync<DeviceDisconnectedException>(() => request.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private sealed class Factory(IConnectionContext context) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(context);
    }
}
