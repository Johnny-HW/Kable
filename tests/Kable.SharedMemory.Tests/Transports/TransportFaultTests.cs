namespace Kable.SharedMemory.Tests.Transports;

using Xunit;

public class TransportFaultTests
{
    [Fact]
    public async Task ConsumerCompletesInput_NextInboundPublicationStopsConnection()
    {
        string name = "reader_fault_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, 256);
        await using var client = SharedMemoryTransport.ConnectClientEndpoint(name);
        CancellationToken closed = server.ConnectionClosed;
        server.Input.Complete();
        await client.Output.WriteAsync(new byte[] { 1 });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Task.Delay(Timeout.Infinite, closed).WaitAsync(TimeSpan.FromSeconds(5)));
        await server.DisposeAsync();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task OutputFails_PumpClosesBothEndpointsAndDisposalFinishes()
    {
        string name = "writer_fault_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, 256);
        await using var client = SharedMemoryTransport.ConnectClientEndpoint(name);
        CancellationToken closed = client.ConnectionClosed;
        client.Output.Complete(new IOException("injected output failure"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Task.Delay(Timeout.Infinite, closed).WaitAsync(TimeSpan.FromSeconds(5)));
        var result = await server.Input.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(result.IsCompleted);
        server.Input.AdvanceTo(result.Buffer.End);
        await client.DisposeAsync();
        await server.DisposeAsync();
    }
}
