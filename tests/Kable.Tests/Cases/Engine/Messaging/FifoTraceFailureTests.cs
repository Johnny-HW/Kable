namespace Kable.Tests.Cases.Engine.Messaging;

using System.Threading.Channels;
using Kable.Codecs;
using Kable.Engine;
using Kable.Observability;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class FifoTraceFailureTests
{
    [Theory]
    [InlineData("REQ_TX")]
    [InlineData("DEVICE_TIMEOUT")]
    public async Task TraceFailure_AfterSend_ClosesSession(string failingTag)
    {
        var factory = new TestMemoryConnectionFactory();
        await using var session = new KableSession<string>(factory, new AsciiLineCodec(), new ThrowingObserver(failingTag));
        await session.StartAsync();
        var request = session.RequestAsync<string>("REQUEST", TimeSpan.FromMilliseconds(300)).AsTask();
        var read = await factory.Context.RemoteRead.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        factory.Context.RemoteRead.AdvanceTo(read.Buffer.End);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request);
        Assert.False(session.IsConnected);
    }

    private sealed class ThrowingObserver(string failingTag) : ICommObserver
    {
        private readonly CommObserver _inner = new();
        public ChannelReader<PacketTraceRecord> PeriodicStream => _inner.PeriodicStream;
        public ChannelReader<PacketTraceRecord> CommandStream => _inner.CommandStream;
        public ChannelReader<PacketTraceRecord> AlarmStream => _inner.AlarmStream;
        public void OnPacketTrace(in PacketTraceRecord trace)
        {
            if (trace.Tag == failingTag) throw new InvalidOperationException("Observer failed");
            _inner.OnPacketTrace(trace);
        }
    }
}
