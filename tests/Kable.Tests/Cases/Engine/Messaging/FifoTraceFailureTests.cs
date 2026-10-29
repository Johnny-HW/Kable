namespace Kable.Tests.Cases.Engine.Messaging;

using System.Threading.Channels;
using Kable.Codecs;
using Kable.Engine;
using Kable.Exceptions;
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
        var observer = new ThrowingObserver(failingTag);
        await using var session = new KableSession<string>(factory, new AsciiLineCodec(), observer);
        await session.StartAsync();
        var request = session.RequestAsync<string>("REQUEST", TimeSpan.FromMilliseconds(300)).AsTask();
        // Observe the send trace, not the pipe that safe-close can already have completed.
        await observer.Sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (failingTag == "DEVICE_TIMEOUT")
        {
            var error = await Assert.ThrowsAsync<DeviceTimeoutException>(() => request);
            Assert.Same(observer.Failure, error.Data["TraceException"]);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => request);
            Assert.Same(observer.Failure, error);
        }
        Assert.False(session.IsConnected);
    }

    private sealed class ThrowingObserver(string failingTag) : ICommObserver
    {
        private readonly CommObserver _inner = new();
        public TaskCompletionSource<bool> Sent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public InvalidOperationException Failure { get; } = new("Observer failed");
        public ChannelReader<PacketTraceRecord> PeriodicStream => _inner.PeriodicStream;
        public ChannelReader<PacketTraceRecord> CommandStream => _inner.CommandStream;
        public ChannelReader<PacketTraceRecord> AlarmStream => _inner.AlarmStream;
        public void OnPacketTrace(in PacketTraceRecord trace)
        {
            if (trace.Tag == "REQ_TX") Sent.TrySetResult(true);
            if (trace.Tag == failingTag) throw Failure;
            _inner.OnPacketTrace(trace);
        }
    }
}
