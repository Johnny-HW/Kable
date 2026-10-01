using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Exceptions;
using Kable.Simple;
using Xunit;

namespace Kable.Tests.Cases.Simple;

public sealed class KableSimpleRoutingTests
{
    [Fact]
    public async Task QueryAsync_WithCustomAutonomousPredicate_CorrectlyReceivesExclamationOkResponse()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(cts.Token);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

            while (!cts.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cts.Token);
                if (line == null) break;

                if (line == "RUN")
                {
                    // Device sends "!OK" as normal command completion
                    await writer.WriteLineAsync("!OK");
                }
                else if (line == "TRIGGER_EVT")
                {
                    // Device sends spontaneous event
                    await writer.WriteLineAsync("$EVT:ALARM");
                }
            }
        }, cts.Token);

        try
        {
            var options = new KableSimpleOptions
            {
                // Only treat "$" as autonomous, so "!OK" is treated as normal command response
                IsAutonomousMessage = msg => msg.StartsWith('$'),
                DefaultTimeout = TimeSpan.FromSeconds(2)
            };

            await using var client = await KableSimple.OpenTcpAsync("127.0.0.1", port, options, cts.Token);

            var eventReceivedTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.LineReceived += line => eventReceivedTcs.TrySetResult(line);

            // 1. Query RUN -> must receive "!OK" as response within timeout
            string response = await client.QueryAsync("RUN", ct: cts.Token);
            response.Should().Be("!OK");

            // 2. Trigger autonomous event -> must be routed to LineReceived
            await client.SendLineAsync("TRIGGER_EVT", cts.Token);
            string receivedEvent = await eventReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2), cts.Token);
            receivedEvent.Should().Be("$EVT:ALARM");
        }
        finally
        {
            cts.Cancel();
            listener.Stop();
            try { await serverTask; } catch { }
        }
    }

    [Fact]
    public async Task QueryAsync_DefaultTimeoutInOptions_IsRespectedWhenTimeoutArgOmitted()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(cts.Token);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            // Server reads command but deliberately does not respond, provoking timeout
            await reader.ReadLineAsync(cts.Token);
            await Task.Delay(5000, cts.Token);
        }, cts.Token);

        try
        {
            var options = new KableSimpleOptions
            {
                DefaultTimeout = TimeSpan.FromMilliseconds(400)
            };

            await using var client = await KableSimple.OpenTcpAsync("127.0.0.1", port, options, cts.Token);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Func<Task> act = async () => await client.QueryAsync("HANG_CMD", ct: cts.Token);
            await act.Should().ThrowAsync<DeviceTimeoutException>();
            sw.Stop();

            // Should time out around 400ms (+ margin), much less than default 3000ms
            sw.ElapsedMilliseconds.Should().BeInRange(300, 2000);
        }
        finally
        {
            cts.Cancel();
            listener.Stop();
            try { await serverTask; } catch { }
        }
    }
}
