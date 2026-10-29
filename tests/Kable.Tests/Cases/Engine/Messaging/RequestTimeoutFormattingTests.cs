namespace Kable.Tests.Cases.Engine.Messaging;

using System.Buffers;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Tests.Fixtures;
using Microsoft.Extensions.Time.Testing;
using Xunit;

public sealed class RequestTimeoutFormattingTests
{
    [Fact]
    public async Task ThrowingToString_DoesNotReplaceTimeout()
    {
        var clock = new FakeTimeProvider();
        var factory = new TestMemoryConnectionFactory();
        var message = new Message();
        var heartbeat = new HeartbeatOptions<Message>(TimeSpan.FromHours(1), TimeSpan.FromHours(2), () => message, timeProvider: clock);
        await using var session = new KableSession<Message>(factory, new Codec(), heartbeatOptions: heartbeat);
        await session.StartAsync();
        var request = session.RequestAsync<Message>(message, TimeSpan.FromMinutes(1)).AsTask();
        await factory.Context.FlushStarted.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromMinutes(2));
        var error = await Assert.ThrowsAsync<DeviceTimeoutException>(() => request.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("UnknownCommand", error.Command);
        Assert.Same(message.Failure, error.Data["CommandFormattingException"]);
        Assert.False(session.IsConnected);
    }

    private sealed class Message
    {
        public Exception Failure { get; } = new InvalidOperationException("Formatting failed");
        public override string ToString() => throw Failure;
    }

    private sealed class Codec : IProtocolCodec<Message>
    {
        public bool SupportsCorrelationId => false;
        public string? ExtractCorrelationId(Message message) => null;
        public bool IsAutonomousMessage(Message message) => false;
        public bool TryDecode(ref ReadOnlySequence<byte> buffer, out Message message) { message = null!; return false; }
        public void Encode(Message message, IBufferWriter<byte> output) { output.GetSpan(1)[0] = 1; output.Advance(1); }
    }
}
