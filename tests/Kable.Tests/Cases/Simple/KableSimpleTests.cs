namespace Kable.Tests.Cases.Simple;

using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Core;
using Kable.Simple;
using Kable.Transports;
using Xunit;

public class KableSimpleTests
{
    [Fact]
    public async Task KableSimple_OpenTcpAndQuery_ReceivesResponse()
    {
        await using var listener = new TcpConnectionListener(IPAddress.Loopback, 0);
        int port = ((IPEndPoint)listener.LocalEndPoint).Port;

        var serverTask = Task.Run(async () =>
        {
            await using var serverCtx = await listener.AcceptAsync();
            var readResult = await serverCtx.Input.ReadAsync();
            serverCtx.Input.AdvanceTo(readResult.Buffer.End);

            byte[] resp = Encoding.ASCII.GetBytes("24.5\n");
            await serverCtx.Output.WriteAsync(resp);
            await serverCtx.Output.FlushAsync();
            await Task.Delay(200);
        });

        await using var client = await KableSimple.OpenTcpAsync("127.0.0.1", port);
        client.IsConnected.Should().BeTrue();

        string answer = await client.QueryAsync("READ_TEMP", TimeSpan.FromSeconds(3));
        answer.Should().Be("24.5");

        await serverTask;
    }

    [Fact]
    public async Task KableSimple_LineReceivedEvent_FiresOnIncomingData()
    {
        await using var listener = new TcpConnectionListener(IPAddress.Loopback, 0);
        int port = ((IPEndPoint)listener.LocalEndPoint).Port;

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientConnectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            await using var serverCtx = await listener.AcceptAsync();
            await clientConnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            byte[] msg = Encoding.ASCII.GetBytes("$EVENT_READY\n");
            await serverCtx.Output.WriteAsync(msg);
            await serverCtx.Output.FlushAsync();
            await Task.Delay(200);
        });

        await using var client = await KableSimple.OpenTcpAsync("127.0.0.1", port);
        client.LineReceived += line => tcs.TrySetResult(line);

        // Signal server that client listener is ready
        clientConnectedTcs.TrySetResult();

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Should().Be("$EVENT_READY");

        await serverTask;
    }

    [Fact]
    public async Task KableSimple_DisconnectedEvent_SurfacesExceptionOnRemoteAbort()
    {
        await using var listener = new TcpConnectionListener(IPAddress.Loopback, 0);
        int port = ((IPEndPoint)listener.LocalEndPoint).Port;

        var disconnectedTcs = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientConnectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            await using var serverCtx = await listener.AcceptAsync();
            await clientConnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Abruptly abort connection
            serverCtx.Abort("Server forced reset");
        });

        await using var client = await KableSimple.OpenTcpAsync("127.0.0.1", port);
        client.Disconnected += ex => disconnectedTcs.TrySetResult(ex);

        clientConnectedTcs.TrySetResult();

        var ex = await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ex.Should().NotBeNull();
        ex.Should().BeAssignableTo<Exception>();

        await serverTask;
    }
}
