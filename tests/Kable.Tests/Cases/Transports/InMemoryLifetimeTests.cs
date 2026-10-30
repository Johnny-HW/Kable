namespace Kable.Tests.Cases.Transports;

using Kable.Transports;
using Xunit;

public sealed class InMemoryLifetimeTests
{
    [Fact]
    public async Task AbortThenDisposeCompletesPeerInput()
    {
        var (client, server) = InMemoryConnectionContext.CreatePair();
        await using var serverScope = server;
        client.Abort("test");
        await client.DisposeAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var read = await server.Input.ReadAsync(deadline.Token);
        Assert.True(read.IsCompleted);
        server.Input.AdvanceTo(read.Buffer.End);
        Assert.True(client.ConnectionClosed.IsCancellationRequested);
        await client.DisposeAsync();
    }
}
