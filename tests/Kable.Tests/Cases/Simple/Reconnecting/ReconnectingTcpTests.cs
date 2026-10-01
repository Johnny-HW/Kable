namespace Kable.Tests.Cases.Simple.Reconnecting;

using System.Net;
using System.Net.Sockets;
using System.Text;
using Kable.Exceptions;
using Kable.Simple;
using Kable.Simple.Reconnecting;
using Xunit;

[Collection("HardwareTransportTests")]
public class ReconnectingTcpTests
{
    [Fact]
    public async Task RemoteDisconnect_RecoversTcp_WithoutReplayingMove()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands = new List<string>();
        var server = Task.Run(async () =>
        {
            using (var first = await listener.AcceptTcpClientAsync(deadline.Token))
            using (var reader = new StreamReader(first.GetStream(), Encoding.ASCII))
                commands.Add((await reader.ReadLineAsync(deadline.Token))!);
            using var second = await listener.AcceptTcpClientAsync(deadline.Token);
            using var stream = second.GetStream();
            using var nextReader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            commands.Add((await nextReader.ReadLineAsync(deadline.Token))!);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("OK\n"), deadline.Token);
            await releaseServer.Task.WaitAsync(deadline.Token);
        });
        try
        {
            await using var client = await ReconnectingKableClient.OpenAsync(
                ct => KableSimple.OpenTcpAsync("127.0.0.1", port, ct: ct),
                new ReconnectOptions { RetryDelay = TimeSpan.FromMilliseconds(20) }, deadline.Token);
            client.Reconnected += () => restored.TrySetResult();
            await Assert.ThrowsAsync<DeviceDisconnectedException>(() =>
                client.QueryAsync("MOVE", ct: deadline.Token).AsTask());
            await restored.Task.WaitAsync(deadline.Token);
            Assert.Equal("OK", await client.QueryAsync("STATUS", ct: deadline.Token));
            releaseServer.TrySetResult();
            await server;
            Assert.Equal(new[] { "MOVE", "STATUS" }, commands);
        }
        finally
        {
            releaseServer.TrySetResult();
            deadline.Cancel();
            listener.Stop();
            try { await server; }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
        }
    }
}
