namespace Kable.Tests.Cases.Engine.Server;

using System;
using System.Buffers;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class KableServerFaultTests
{
    private sealed class FaultCodec : IProtocolCodec<string>
    {
        private readonly bool _consumeGarbage;
        public FaultCodec(bool consumeGarbage) => _consumeGarbage = consumeGarbage;
        public bool SupportsCorrelationId => false;
        public bool TryDecode(ref ReadOnlySequence<byte> buffer, out string message)
        {
            message = "MESSAGE";
            if (_consumeGarbage)
            {
                byte first = buffer.FirstSpan[0];
                buffer = buffer.Slice(1);
                return first != 0;
            }
            return true; // Fault injection: success without consumption must terminate the session.
        }
        public void Encode(string message, IBufferWriter<byte> output) => throw new InvalidOperationException("encode fault");
        public string? ExtractCorrelationId(string message) => null;
        public bool IsAutonomousMessage(string message) => true;
    }

    [Fact]
    public async Task DecoderSuccessWithoutProgress_DisconnectsInsteadOfSpinning()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new FaultCodec(false));
        var disconnected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientDisconnected += (_, args) => disconnected.TrySetResult(args.Reason);
        await server.StartAsync();
        var (context, client) = TestDuplexPipePair.Create();
        await using var owned = client;
        listener.EnqueueConnection(context);
        await client.WriteAsciiLineAsync("BAD");
        (await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(3))).Should().Contain("no progress");
        server.ActiveSessionCount.Should().Be(0);
    }

    [Fact]
    public async Task DecoderFalseAfterConsumedGarbage_RetriesBufferedFrameImmediately()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new FaultCodec(true));
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += (_, args) => received.TrySetResult(args.Message);
        await server.StartAsync();
        var (context, client) = TestDuplexPipePair.Create();
        await using var owned = client;
        listener.EnqueueConnection(context);
        await client.Output.WriteAsync(new byte[] { 0, 1 });
        (await received.Task.WaitAsync(TimeSpan.FromSeconds(3))).Should().Be("MESSAGE");
    }

    [Fact]
    public async Task EncoderFailure_DisconnectsAndReportsReason()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new FaultCodec(true));
        var connected = new TaskCompletionSource<IServerSession<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += (_, args) => connected.TrySetResult(args.Session);
        server.ClientDisconnected += (_, args) => disconnected.TrySetResult(args.Reason);
        await server.StartAsync();
        var (context, client) = TestDuplexPipePair.Create();
        await using var owned = client;
        listener.EnqueueConnection(context);
        await (await connected.Task.WaitAsync(TimeSpan.FromSeconds(3))).SendAsync("FAIL");
        (await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(3))).Should().Be("encode fault");
        context.ConnectionClosed.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task ThrowingEventSubscriber_DoesNotSkipOtherSubscribers()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new AsciiLineCodec());
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += (_, _) => throw new InvalidOperationException("handler fault");
        server.MessageReceived += (_, args) => received.TrySetResult(args.Message);
        await server.StartAsync();
        var (context, client) = TestDuplexPipePair.Create();
        await using var owned = client;
        listener.EnqueueConnection(context);
        await client.WriteAsciiLineAsync("NEXT");
        (await received.Task.WaitAsync(TimeSpan.FromSeconds(3))).Should().Be("NEXT");
    }
}
