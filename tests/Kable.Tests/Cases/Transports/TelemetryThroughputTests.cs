namespace Kable.Tests.Cases;

using System;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Extensions;
using Kable.Tests.Fixtures;
using Kable.Transports;
using Xunit;

[Collection("HardwareTransportTests")]
public sealed class TelemetryThroughputTests
{
    [Fact]
    public async Task TC_TRN_08_TelemetryStreaming_HighThroughput_ZeroGen2GarbageCollections()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        const int packetCount = 2000;
        var serverTask = Task.Run(async () =>
        {
            using var serverSocket = await listener.AcceptSocketAsync();
            var payload = Encoding.ASCII.GetBytes("$TELEMETRY,123.456,OK\n");
            for (int i = 0; i < packetCount; i++)
            {
                serverSocket.Send(payload);
            }
            await Task.Delay(100);
            serverSocket.Shutdown(SocketShutdown.Both);
        });

        try
        {
            await using var session = new KableClientBuilder<string>()
                .UseTcp("127.0.0.1", port)
                .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
                .Build();

            await session.StartAsync();

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            int gen2Before = GC.CollectionCount(2);

            int received = 0;
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            await foreach (var item in session.Stream.WithCancellation(cts.Token))
            {
                received++;
                if (received >= packetCount) break;
            }

            int gen2After = GC.CollectionCount(2);

            received.Should().Be(packetCount);
            (gen2After - gen2Before).Should().BeLessThanOrEqualTo(1, "High-throughput streaming should not trigger excessive Gen2 GC collections");
        }
        finally
        {
            try { await serverTask; } catch { }
            listener.Stop();
        }
    }
}
