namespace Kable.Tests.Cases.Engine;

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Transports;
using Xunit;

public class SessionTimeoutAndRaceConditionTests
{
    private sealed class CidAsciiCodec : IProtocolCodec<string>
    {
        public bool SupportsCorrelationId => true;
        public int MaxFrameSize => 256;

        public void Encode(string message, IBufferWriter<byte> output)
        {
            var bytes = Encoding.ASCII.GetBytes(message + "\n");
            output.Write(bytes);
        }

        public bool TryDecode(ref ReadOnlySequence<byte> buffer, out string message)
        {
            var reader = new SequenceReader<byte>(buffer);
            if (reader.TryReadTo(out ReadOnlySequence<byte> line, (byte)'\n'))
            {
                message = Encoding.ASCII.GetString(line.ToArray());
                buffer = buffer.Slice(reader.Position);
                return true;
            }

            message = string.Empty;
            return false;
        }

        public string? ExtractCorrelationId(string message)
        {
            // Format: "CID:xxx:body"
            if (message.StartsWith("CID:", StringComparison.Ordinal))
            {
                int nextColon = message.IndexOf(':', 4);
                if (nextColon > 4)
                {
                    return message.Substring(4, nextColon - 4);
                }
            }
            return null;
        }

        public bool IsAutonomousMessage(string message) => false;
    }

    private sealed class MockFactory(IConnectionContext ctx) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(ctx);
    }

    [Fact]
    public async Task RequestAsync_LateArrivingCidResponse_DoesNotCorruptSubsequentRequest()
    {
        var (client, server) = InMemoryConnectionContext.CreatePair();
        var session = new KableSession<string>(new MockFactory(client), new CidAsciiCodec());
        await session.StartAsync();

        try
        {
            // 1. Send CID request with tight timeout
            var timeoutTask = session.RequestAsync<string>("CID:100:REQ_TIMEOUT", TimeSpan.FromMilliseconds(50));
            Func<Task> act = async () => await timeoutTask;
            await act.Should().ThrowAsync<DeviceTimeoutException>();

            // Session remains running in CID full-duplex mode
            session.IsConnected.Should().BeTrue();

            // 2. Late response for CID 100 arrives after timeout
            await server.Output.WriteAsync(Encoding.ASCII.GetBytes("CID:100:LATE_RESPONSE\n"));
            await Task.Delay(50);

            // 3. Send a new request with CID 101 and answer it
            var echoTask = Task.Run(async () =>
            {
                var reader = new SequenceReader<byte>((await server.Input.ReadAsync()).Buffer);
                if (reader.TryReadTo(out ReadOnlySequence<byte> line, (byte)'\n'))
                {
                    server.Input.AdvanceTo(reader.Position);
                    await server.Output.WriteAsync(Encoding.ASCII.GetBytes("CID:101:VALID_RESPONSE\n"));
                }
            });

            var result = await session.RequestAsync<string>("CID:101:REQ_OK", TimeSpan.FromSeconds(2));
            await echoTask;

            result.Should().Be("CID:101:VALID_RESPONSE");
        }
        finally
        {
            await session.DisposeAsync();
            await server.DisposeAsync();
        }
    }
}
