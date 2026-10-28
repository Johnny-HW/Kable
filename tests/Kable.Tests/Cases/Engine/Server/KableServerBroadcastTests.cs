namespace Kable.Tests.Cases.Engine.Server;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class KableServerBroadcastTests
{
    private sealed class LineCodec : IProtocolCodec<string>
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

    [Fact]
    public async Task BroadcastAsync_WhenNoClients_ReturnsZeroResult()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new LineCodec());
        await server.StartAsync();

        var result = await server.BroadcastAsync("BROADCAST_EMPTY");

        result.TotalSessions.Should().Be(0);
        result.EnqueuedCount.Should().Be(0);
        result.FailedConnectionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task BroadcastAsync_ToMultipleClients_DeliversToAllAndReturnsSuccessResult()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new LineCodec());

        var connectedCount = 0;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += (_, _) =>
        {
            if (Interlocked.Increment(ref connectedCount) == 3)
            {
                tcs.TrySetResult(true);
            }
        };

        await server.StartAsync();

        var (s1, c1) = TestDuplexPipePair.Create();
        var (s2, c2) = TestDuplexPipePair.Create();
        var (s3, c3) = TestDuplexPipePair.Create();
        listener.EnqueueConnection(s1);
        listener.EnqueueConnection(s2);
        listener.EnqueueConnection(s3);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        server.ActiveSessionCount.Should().Be(3);

        var result = await server.BroadcastAsync("NOTIFY_ALL");

        result.TotalSessions.Should().Be(3);
        result.EnqueuedCount.Should().Be(3);
        result.FailedConnectionIds.Should().BeEmpty();

        // Verify each client received the message
        var read1 = await c1.Input.ReadAsync();
        Encoding.UTF8.GetString(read1.Buffer.ToArray()).Should().Be("NOTIFY_ALL\n");

        var read2 = await c2.Input.ReadAsync();
        Encoding.UTF8.GetString(read2.Buffer.ToArray()).Should().Be("NOTIFY_ALL\n");

        var read3 = await c3.Input.ReadAsync();
        Encoding.UTF8.GetString(read3.Buffer.ToArray()).Should().Be("NOTIFY_ALL\n");
    }

    [Fact]
    public async Task BroadcastAsync_WhenOneClientIsSlow_EnforcesTimeoutAndIsolatesFailure()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(
            listener,
            () => new LineCodec(),
            new KableServerOptions { SessionQueueCapacity = 1, DefaultBroadcastTimeout = TimeSpan.FromMilliseconds(100) });

        var connectedCount = 0;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += (_, _) =>
        {
            if (Interlocked.Increment(ref connectedCount) == 2)
            {
                tcs.TrySetResult(true);
            }
        };

        await server.StartAsync();

        var (sNormal, cNormal) = TestDuplexPipePair.Create();
        // Slow client with tiny queue that will quickly fill up
        var (sSlow, cSlow) = TestDuplexPipePair.Create(
            serverToClientOptions: new PipeOptions(pauseWriterThreshold: 1, resumeWriterThreshold: 1));

        listener.EnqueueConnection(sNormal);
        listener.EnqueueConnection(sSlow);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

        // Pre-fill slow client queue to trigger wait/timeout on next broadcast
        if (server.TryGetSession(sSlow.ConnectionId, out var slowSession) && slowSession != null)
        {
            await slowSession.SendAsync("FILL_1");
            await slowSession.SendAsync("FILL_2");
            using var fillTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            Func<Task> fill = async () => await slowSession.SendAsync("FILL_3", fillTimeout.Token);
            await fill.Should().ThrowAsync<OperationCanceledException>();
        }

        // Broadcast with a short timeout to verify slow client is isolated
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var result = await server.BroadcastAsync("TEST_SLOW", cts.Token);

        result.TotalSessions.Should().Be(2);
        result.EnqueuedCount.Should().Be(1);
        result.FailedConnectionIds.Should().Equal(sSlow.ConnectionId);
    }
}
