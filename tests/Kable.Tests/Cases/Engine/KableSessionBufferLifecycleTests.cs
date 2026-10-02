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
using Kable.Transports;
using Xunit;

public sealed class KableSessionBufferLifecycleTests
{
    private sealed class MultiSegmentBufferCodec : IProtocolCodec<string>
    {
        public bool SupportsCorrelationId => false;
        public int MaxFrameSize => 1024;

        public void Encode(string message, IBufferWriter<byte> output)
        {
            var bytes = Encoding.UTF8.GetBytes(message + "\n");
            output.Write(bytes);
        }

        public bool TryDecode(ref ReadOnlySequence<byte> buffer, out string message)
        {
            var reader = new SequenceReader<byte>(buffer);
            if (reader.TryReadTo(out ReadOnlySequence<byte> line, (byte)'\n'))
            {
                message = Encoding.UTF8.GetString(line.ToArray());
                buffer = buffer.Slice(reader.Position);
                return true;
            }

            message = string.Empty;
            return false;
        }

        public string? ExtractCorrelationId(string message) => null;
        public bool IsAutonomousMessage(string message) => false;
    }

    [Fact]
    public async Task KableSession_MultiSegmentArrival_ShouldConsumeCleanlyWithoutBufferLeak()
    {
        var (client, server) = InMemoryConnectionContext.CreatePair();
        var session = new KableSession<string>(new DirectFactory(client), new MultiSegmentBufferCodec());

        await session.StartAsync();

        try
        {
            // Write partial frame 1
            await server.Output.WriteAsync(Encoding.UTF8.GetBytes("Part1_"));
            await Task.Delay(20);

            // Write partial frame 2 completing the line
            await server.Output.WriteAsync(Encoding.UTF8.GetBytes("Part2_Completed\n"));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await foreach (var item in session.GetStreamAsync(cts.Token))
            {
                item.Should().Be("Part1_Part2_Completed");
                break;
            }
        }
        finally
        {
            await session.DisposeAsync();
            await server.DisposeAsync();
        }
    }

    private sealed class DirectFactory(IConnectionContext context) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(context);
    }
}
