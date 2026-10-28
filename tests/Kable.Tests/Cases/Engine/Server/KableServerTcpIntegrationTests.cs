namespace Kable.Tests.Cases.Engine.Server;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Builder;
using Kable.Codecs;
using Kable.Core;
using Xunit;

public sealed class KableServerTcpIntegrationTests
{
    private sealed class EchoAsciiCodec : IProtocolCodec<string>
    {
        public bool SupportsCorrelationId => false;

        public bool TryDecode(ref ReadOnlySequence<byte> buffer, out string message)
        {
            var reader = new SequenceReader<byte>(buffer);
            if (reader.TryReadTo(out ReadOnlySequence<byte> line, 0x0A))
            {
                message = Encoding.UTF8.GetString(BuffersExtensions.ToArray(line));
                buffer = buffer.Slice(reader.Position);
                return true;
            }
            message = string.Empty;
            return false;
        }

        public void Encode(string message, IBufferWriter<byte> output)
        {
            var bytes = Encoding.UTF8.GetBytes(message + "\n");
            BuffersExtensions.Write(output, bytes.AsSpan());
        }

        public string? ExtractCorrelationId(string message) => null;
        public bool IsAutonomousMessage(string message) => true;
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task RealTcp_ClientsConnect_SendAndReceiveMessages_AndDisconnect()
    {
        int port = GetAvailablePort();

        await using var server = new KableServerBuilder<string>()
            .ListenTcp(IPAddress.Loopback, port)
            .UseCodec(() => new EchoAsciiCodec())
            .ConfigureOptions(opt => opt.SessionQueueCapacity = 500)
            .Build();

        var messageTcs = new TaskCompletionSource<(string ConnId, string Text)>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += (_, e) => messageTcs.TrySetResult((e.Session.ConnectionId, e.Message));

        await server.StartAsync();

        // Connect real TCP client
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, port);
        using var networkStream = tcpClient.GetStream();

        // Send line
        var payload = Encoding.UTF8.GetBytes("PING_TCP\n");
        await networkStream.WriteAsync(payload);
        await networkStream.FlushAsync();

        var (connId, text) = await messageTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        connId.Should().NotBeNullOrEmpty();
        text.Should().Be("PING_TCP");

        // Server replies
        await server.SendAsync(connId, "PONG_TCP");

        // Client reads reply
        var buffer = new byte[256];
        int readBytes = await networkStream.ReadAsync(buffer, 0, buffer.Length);
        var reply = Encoding.UTF8.GetString(buffer, 0, readBytes);
        reply.Should().Be("PONG_TCP\n");

        await server.StopAsync(TimeSpan.FromSeconds(1));
        server.IsRunning.Should().BeFalse();
    }
}


