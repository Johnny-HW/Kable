namespace Kable.SharedMemory.Tests.Connections;

using System.IO.Pipes;
using Kable.SharedMemory.Connections;
using Xunit;

public class HandshakeFailureTests
{
    [Fact]
    public async Task MalformedRequest_IsRejectedAndNextClientCanConnect()
    {
        string name = "bad_request_" + Guid.NewGuid().ToString("N");
        await using var listener = new SharedMemoryServerListener(name);
        Task accept = listener.AcceptAsync().AsTask();
        using (var invalid = new NamedPipeClientStream(".", SharedMemoryHandshake.ControlName(name),
            PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await invalid.ConnectAsync(5000);
            await invalid.WriteAsync(new byte[8]);
            await Assert.ThrowsAsync<InvalidDataException>(() => accept);
        }
        var (server, client) = await ManagedConnectionTests.ConnectAsync(listener, name);
        await client.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task SilentHandshake_TimesOutWithoutStoppingListener()
    {
        string name = "silent_" + Guid.NewGuid().ToString("N");
        await using var listener = new SharedMemoryServerListener(name, handshakeTimeoutMs: 100);
        Task accept = listener.AcceptAsync().AsTask();
        using (var silent = new NamedPipeClientStream(".", SharedMemoryHandshake.ControlName(name),
            PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await silent.ConnectAsync(5000);
            await Assert.ThrowsAsync<TimeoutException>(() => accept.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        var (server, client) = await ManagedConnectionTests.ConnectAsync(listener, name);
        await client.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task ClientConnection_CanBeCanceledAndDoesNotRetryForever()
    {
        string name = "absent_" + Guid.NewGuid().ToString("N");
        using var cancel = new CancellationTokenSource();
        Task pending = new SharedMemoryClientFactory(name).ConnectAsync(cancel.Token).AsTask();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await Assert.ThrowsAsync<TimeoutException>(() => new SharedMemoryClientFactory(name, 50).ConnectAsync().AsTask());
    }
}
