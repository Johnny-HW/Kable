using System.Net;
using System.Net.Sockets;
using System.Text;
using Kable.Simple;

namespace Kable.QuickStart;

/// <summary>
/// Kable 10-Minute Onboarding QuickStart Sample
/// Demonstrates spinning up a local mock device and interacting via KableSimple.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Kable QuickStart Demo ===");

        // 1. Start a local mock equipment server on loopback
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Console.WriteLine($"[Mock Server] Listening on 127.0.0.1:{port}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var serverTask = Task.Run(() => RunMockServerAsync(listener, cts.Token));

        try
        {
            // 2. Connect using KableSimple (Zero-Boilerplate 3-line facade)
            Console.WriteLine("[KableSimple] Connecting to device...");
            await using var client = await KableSimple.OpenTcpAsync("127.0.0.1", port, ct: cts.Token);
            Console.WriteLine($"[KableSimple] Connected: {client.IsConnected}");

            var eventReceivedTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            // 3. Register spontaneous event listener (Autonomous messages prefixed by $, #, !, *)
            client.LineReceived += line =>
            {
                Console.WriteLine($"[Event Received] {line}");
                if (line.StartsWith("$EVT:"))
                {
                    eventReceivedTcs.TrySetResult(line);
                }
            };

            client.Disconnected += ex =>
            {
                Console.WriteLine($"[Disconnected] Reason: {ex?.Message ?? "Normal closure"}");
            };

            // 4. Query-Response cycle (Strict FIFO synchronous exchange)
            Console.WriteLine("[Query 1] Sending *IDN?...");
            string idn = await client.QueryAsync("*IDN?", TimeSpan.FromSeconds(2), cts.Token);
            Console.WriteLine($"[Response 1] {idn}");

            Console.WriteLine("[Query 2] Sending STATUS?...");
            string status = await client.QueryAsync("STATUS?", TimeSpan.FromSeconds(2), cts.Token);
            Console.WriteLine($"[Response 2] {status}");

            // 5. Trigger an autonomous hardware event from equipment
            Console.WriteLine("[Command] Requesting equipment to trigger event...");
            await client.SendLineAsync("TRIGGER_EVENT", cts.Token);

            // Await event arrival with timeout
            using var eventTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, eventTimeoutCts.Token);
            linkedCts.Token.Register(() => eventReceivedTcs.TrySetCanceled());

            string receivedEvent = await eventReceivedTcs.Task;
            Console.WriteLine($"[Verified Event] {receivedEvent}");

            Console.WriteLine("=== QuickStart Completed Successfully! ===");
        }
        finally
        {
            cts.Cancel();
            listener.Stop();
            try { await serverTask; } catch { /* ignore server stop exception */ }
        }
    }

    private static async Task RunMockServerAsync(TcpListener listener, CancellationToken ct)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync(ct);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

            while (!ct.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                switch (line.Trim())
                {
                    case "*IDN?":
                        await writer.WriteLineAsync("Kable Virtual Equipment v1.0");
                        break;
                    case "STATUS?":
                        await writer.WriteLineAsync("STATUS:READY");
                        break;
                    case "TRIGGER_EVENT":
                        // Echo spontaneous event
                        await writer.WriteLineAsync("$EVT:WAFER_LOADED_OK");
                        break;
                    default:
                        await writer.WriteLineAsync($"ECHO:{line}");
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }
}
