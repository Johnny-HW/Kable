using System.Net;
using System.Net.Sockets;
using System.Text;
using Kable.Builder;
using Kable.Codecs;
using Kable.Transports;

internal static class ServerProbe
{
    internal static async Task RunAsync(CancellationToken ct)
    {
        var listener = new TcpConnectionListener(IPAddress.Loopback, 0);
        int port = ((IPEndPoint)listener.LocalEndPoint).Port;
        await using var server = new KableServerBuilder<string>()
            .WithListener(listener)
            .UseCodec(() => new AsciiLineCodec())
            .ConfigureOptions(options => options.MaxConnections = 2)
            .Build();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += (_, args) => received.TrySetResult(args.Message);
        await server.StartAsync(ct);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, ct);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("PING\n"), ct);
        if (await received.Task.WaitAsync(ct) != "PING") throw new Exception("Packaged server receive failed.");
        var result = await server.BroadcastAsync("PONG", ct);
        if (result.EnqueuedCount != 1 || result.FailedConnectionIds.Count != 0)
            throw new Exception("Packaged server broadcast failed.");
        if (await reader.ReadLineAsync(ct) != "PONG") throw new Exception("Packaged server send failed.");
        await server.StopAsync(ct: ct);
        if (server.ActiveSessionCount != 0) throw new Exception("Packaged server cleanup failed.");
    }
}
