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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidOrMissingAck_ReleasesGenerationAndAllowsNextConnection(bool disconnect)
    {
        string name = "bad_ack_" + Guid.NewGuid().ToString("N");
        await using var listener = new SharedMemoryServerListener(name);
        Task accept = listener.AcceptAsync().AsTask();
        using (var invalid = new NamedPipeClientStream(".", SharedMemoryHandshake.ControlName(name),
            PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await invalid.ConnectAsync(5000);
            await SharedMemoryHandshake.WriteRequestAsync(invalid, CancellationToken.None);
            Guid generation = await SharedMemoryHandshake.ReadReplyAsync(invalid, CancellationToken.None);
            using var held = Kable.SharedMemory.Memory.SharedMemoryRingBuffer.Open(
                SharedMemoryHandshake.SessionName(name, generation) + "_s2c");
            if (disconnect)
            {
                invalid.Dispose();
                await Assert.ThrowsAsync<EndOfStreamException>(() => accept);
            }
            else
            {
                await invalid.WriteAsync(new byte[] { 0 });
                await Assert.ThrowsAsync<InvalidDataException>(() => accept);
            }
            Assert.True(held.IsClosed);
        }
        var (server, client) = await ManagedConnectionTests.ConnectAsync(listener, name);
        await client.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task MalformedServerReply_IsRejectedAndControlPipeCloses()
    {
        string name = "bad_reply_" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(SharedMemoryHandshake.ControlName(name), PipeDirection.InOut,
            1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        Task connect = new SharedMemoryClientFactory(name).ConnectAsync().AsTask();
        await server.WaitForConnectionAsync();
        await SharedMemoryHandshake.ReadRequestAsync(server, CancellationToken.None);
        await server.WriteAsync(new byte[24]);
        await Assert.ThrowsAsync<InvalidDataException>(() => connect);
        Assert.Equal(0, await server.ReadAsync(new byte[1]));
    }

    [Fact]
    public async Task ManyCanceledAccepts_DoNotBlockListenerDisposal()
    {
        string name = "queued_accept_" + Guid.NewGuid().ToString("N");
        var listener = new SharedMemoryServerListener(name);
        Task[] pending = Enumerable.Range(0, 16).Select(_ => listener.AcceptAsync().AsTask()).ToArray();
        await listener.DisposeAsync();
        foreach (Task task in pending)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
