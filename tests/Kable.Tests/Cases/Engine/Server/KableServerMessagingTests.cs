namespace Kable.Tests.Cases.Engine.Server;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class KableServerMessagingTests
{
    private sealed class TrackingCodec : IProtocolCodec<string>
    {
        public static int InstanceCounter;
        public int InstanceId { get; } = Interlocked.Increment(ref InstanceCounter);

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
    public async Task ClientMessage_DispatchesToMessageReceived_WithSession()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new TrackingCodec());

        var receivedTcs = new TaskCompletionSource<(string connId, string msg)>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += (_, e) => receivedTcs.TrySetResult((e.Session.ConnectionId, e.Message));

        await server.StartAsync();

        var (serverSide, clientSide) = TestDuplexPipePair.Create();
        listener.EnqueueConnection(serverSide);

        await clientSide.WriteAsciiLineAsync("HELLO_SERVER", 0x0A);

        var (connId, msg) = await receivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        connId.Should().Be(serverSide.ConnectionId);
        msg.Should().Be("HELLO_SERVER");
    }

    [Fact]
    public async Task ServerSendAsync_WritesEncodedBytesToClientOutput()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new TrackingCodec());

        var connectedTcs = new TaskCompletionSource<IServerSession<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += (_, e) => connectedTcs.TrySetResult(e.Session);

        await server.StartAsync();

        var (serverSide, clientSide) = TestDuplexPipePair.Create();
        listener.EnqueueConnection(serverSide);

        var session = await connectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await session.SendAsync("WELCOME_CLIENT");

        var readResult = await clientSide.Input.ReadAsync();
        var text = Encoding.UTF8.GetString(readResult.Buffer.ToArray());
        text.Should().Be("WELCOME_CLIENT\n");
    }

    [Fact]
    public async Task MessageReceived_HandlerException_DoesNotCrashServerOrSession()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new TrackingCodec());

        var callCount = 0;
        var secondMessageTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.MessageReceived += (_, e) =>
        {
            var count = Interlocked.Increment(ref callCount);
            if (count == 1)
            {
                throw new InvalidOperationException("Boom in user handler!");
            }
            secondMessageTcs.TrySetResult(e.Message);
        };

        await server.StartAsync();

        var (serverSide, clientSide) = TestDuplexPipePair.Create();
        listener.EnqueueConnection(serverSide);

        // First message causes exception in handler
        await clientSide.WriteAsciiLineAsync("MSG_1", 0x0A);

        // Second message should still be processed normally
        await clientSide.WriteAsciiLineAsync("MSG_2", 0x0A);

        var secondMsg = await secondMessageTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        secondMsg.Should().Be("MSG_2");
        server.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task CodecInstance_IsIsolatedPerSession()
    {
        var initialCounter = TrackingCodec.InstanceCounter;
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new TrackingCodec());

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

        var (serverSide1, clientSide1) = TestDuplexPipePair.Create();
        var (serverSide2, clientSide2) = TestDuplexPipePair.Create();
        listener.EnqueueConnection(serverSide1);
        listener.EnqueueConnection(serverSide2);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

        // Each session should have received a separate codec instance
        (TrackingCodec.InstanceCounter - initialCounter).Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task SessionMessages_PreserveFIFOOrder()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new TrackingCodec());

        var receivedList = new List<string>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.MessageReceived += (_, e) =>
        {
            lock (receivedList)
            {
                receivedList.Add(e.Message);
                if (receivedList.Count == 5)
                {
                    tcs.TrySetResult(true);
                }
            }
        };

        await server.StartAsync();

        var (serverSide, clientSide) = TestDuplexPipePair.Create();
        listener.EnqueueConnection(serverSide);

        for (int i = 1; i <= 5; i++)
        {
            await clientSide.WriteAsciiLineAsync($"SEQ_{i}", 0x0A);
        }

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

        receivedList.Should().Equal("SEQ_1", "SEQ_2", "SEQ_3", "SEQ_4", "SEQ_5");
    }
}
