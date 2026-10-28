namespace Kable.Host;

using System.Buffers;
using System.Text;
using Kable.Transport.Ipc;

internal static class HostSmoke
{
    internal static async Task RunAsync()
    {
        string name = "KableHostSmoke_" + Guid.NewGuid().ToString("N");
        var daemon = new DaemonService(name);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var running = daemon.RunAsync(stop.Token);
        try
        {
            await using var client = await new IpcNamedPipeClientFactory(name).ConnectAsync(stop.Token);
            byte[] expected = Encoding.ASCII.GetBytes("KABLE_HOST_SMOKE\n");
            await client.Output.WriteAsync(expected, stop.Token);
            while (true)
            {
                var received = await client.Input.ReadAsync(stop.Token);
                if (received.Buffer.Length >= expected.Length)
                {
                    if (!received.Buffer.Slice(0, expected.Length).ToArray().AsSpan().SequenceEqual(expected))
                        throw new InvalidOperationException("Host IPC echo mismatch.");
                    client.Input.AdvanceTo(received.Buffer.End);
                    break;
                }
                client.Input.AdvanceTo(received.Buffer.Start, received.Buffer.End);
                if (received.IsCompleted) throw new IOException("Host IPC closed before its reply.");
            }
        }
        finally { stop.Cancel(); await running; }
        if (daemon.ActiveSessionCount != 0) throw new InvalidOperationException("Host retained sessions after shutdown.");
        Console.WriteLine("Host IPC smoke OK.");
    }
}
