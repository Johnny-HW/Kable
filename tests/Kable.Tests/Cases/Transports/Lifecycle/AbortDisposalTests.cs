namespace Kable.Tests.Cases.Transports.Lifecycle;

using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using Kable.Core;
using Kable.Transports;
using Xunit;

public sealed class AbortDisposalTests
{
    [Fact]
    public async Task Tcp_abort_then_dispose_completes_pipelines_and_keeps_closed_token_readable()
    {
        using var listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0)); listener.Listen(1);
        using var peer = new Socket(SocketType.Stream, ProtocolType.Tcp);
        var accept = listener.AcceptAsync();
        await peer.ConnectAsync(listener.LocalEndPoint!);
        var context = new TcpConnectionContext(await accept);
        await AssertDisposedAsync(context);
    }

    [Fact]
    public async Task Pipe_abort_then_dispose_completes_pipelines_and_keeps_closed_token_readable()
    {
        var name = "kable-lifetime-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connected = server.WaitForConnectionAsync();
        var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(); await connected;
        await AssertDisposedAsync(new NamedPipeConnectionContext(client, name));
    }

    private static async Task AssertDisposedAsync(IConnectionContext context)
    {
        context.Output.GetMemory(16); // A buffered writer must release its rented storage even after Abort.
        context.Abort("test");
        var first = context.DisposeAsync().AsTask();
        var second = context.DisposeAsync().AsTask();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(context.ConnectionClosed.IsCancellationRequested);
        Assert.Throws<InvalidOperationException>(() => context.Output.GetMemory(1));
        context.Abort("already disposed");
    }
}
