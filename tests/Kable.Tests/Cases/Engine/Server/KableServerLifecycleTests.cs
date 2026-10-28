namespace Kable.Tests.Cases.Engine.Server;

using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class KableServerLifecycleTests
{
    private sealed class SimpleTestCodec : IProtocolCodec<string>
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
    public async Task StartAndStop_ChangesRunningStateAndStopsListener()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new SimpleTestCodec());

        server.IsRunning.Should().BeFalse();
        await server.StartAsync();
        server.IsRunning.Should().BeTrue();

        await server.StopAsync(TimeSpan.FromSeconds(1));
        server.IsRunning.Should().BeFalse();
        listener.IsStopped.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleClients_ConnectAndDisconnect_UpdatesSessionCountAndFiresEvents()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new SimpleTestCodec());

        var connectedTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnectedTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.ClientConnected += (_, e) => connectedTcs.TrySetResult(e.Session.ConnectionId);
        server.ClientDisconnected += (_, e) => disconnectedTcs.TrySetResult(e.Session.ConnectionId);

        await server.StartAsync();

        var (serverSide, clientSide) = TestDuplexPipePair.Create();
        listener.EnqueueConnection(serverSide);

        var connId = await connectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        connId.Should().Be(serverSide.ConnectionId);
        server.ActiveSessionCount.Should().Be(1);
        server.TryGetSession(connId, out var session).Should().BeTrue();
        session!.IsConnected.Should().BeTrue();

        // Disconnect remote client
        await clientSide.DisposeAsync();

        var discConnId = await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        discConnId.Should().Be(connId);
        server.ActiveSessionCount.Should().Be(0);
        server.TryGetSession(connId, out _).Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WithActiveSessions_TerminatesSessionsAndAbortsWithinTimeout()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new SimpleTestCodec());
        await server.StartAsync();

        var (serverSide, clientSide) = TestDuplexPipePair.Create();
        var connectedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += (_, _) => connectedTcs.TrySetResult(true);

        listener.EnqueueConnection(serverSide);
        await connectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

        server.ActiveSessionCount.Should().Be(1);

        // Stop with a short timeout to enforce Abort cleanup
        await server.StopAsync(TimeSpan.FromMilliseconds(200));

        server.IsRunning.Should().BeFalse();
        server.ActiveSessionCount.Should().Be(0);
        serverSide.ConnectionClosed.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_ConcurrentCalls_AreSafeAndIdempotent()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new SimpleTestCodec());
        await server.StartAsync();

        var t1 = Task.Run(() => server.StopAsync(TimeSpan.FromSeconds(1)));
        var t2 = Task.Run(() => server.StopAsync(TimeSpan.FromSeconds(1)));

        await Task.WhenAll(t1, t2);
        server.IsRunning.Should().BeFalse();
    }
}
