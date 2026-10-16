namespace Kable.Tests.Cases.Engine.Messaging;

using System.Threading.Channels;
using Kable.Engine;
using Kable.Observability;
using Kable.Tests.Fixtures;
using Kable.Tests.Fixtures.Codecs;
using Xunit;

public sealed class CidOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousRequestCleanup_DoesNotRemoveReusedCid(bool invalidResponseType)
    {
        var factory = new TestMemoryConnectionFactory();
        var observer = new ReusingObserver();
        await using var session = new KableSession<string>(factory, new CorrelationIdLineCodec(), observer);
        observer.Session = session;
        await session.StartAsync();

        Task first = invalidResponseType
            ? session.RequestAsync<int>("100:REQ1", TimeSpan.FromSeconds(5)).AsTask()
            : session.RequestAsync<string>("100:REQ1", TimeSpan.FromSeconds(5)).AsTask();
        await ReadTransmittedAsync(factory);
        await factory.Context.WriteAsciiLineAsync("100:RESP1");

        // The response callback reserves B before A reaches its finally, without sleeps.
        if (invalidResponseType)
            await Assert.ThrowsAsync<InvalidCastException>(() => first);
        else
            await first.WaitAsync(TimeSpan.FromSeconds(5));

        var second = Assert.IsAssignableFrom<Task<string>>(observer.Second);
        await ReadTransmittedAsync(factory);
        await factory.Context.WriteAsciiLineAsync("100:RESP2");
        Assert.Equal("100:RESP2", await second.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task CanceledRequest_RemovesOwnEntry_AndAllowsSameCidAgain()
    {
        var factory = new TestMemoryConnectionFactory();
        await using var session = new KableSession<string>(factory, new CorrelationIdLineCodec());
        await session.StartAsync();
        using var cancellation = new CancellationTokenSource();
        var first = session.RequestAsync<string>("100:REQ1", TimeSpan.FromSeconds(5), cancellation.Token).AsTask();
        await ReadTransmittedAsync(factory);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        var second = session.RequestAsync<string>("100:REQ2", TimeSpan.FromSeconds(5)).AsTask();
        await ReadTransmittedAsync(factory);
        await factory.Context.WriteAsciiLineAsync("100:RESP2");
        Assert.Equal("100:RESP2", await second.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(session.IsConnected);
    }

    private static async Task ReadTransmittedAsync(TestMemoryConnectionFactory factory)
    {
        var read = await factory.Context.RemoteRead.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(read.Buffer.IsEmpty);
        factory.Context.RemoteRead.AdvanceTo(read.Buffer.End);
    }

    private sealed class ReusingObserver : ICommObserver
    {
        private readonly CommObserver _inner = new();
        private int _reused;
        public KableSession<string> Session { get; set; } = null!;
        public Task<string>? Second { get; private set; }
        public ChannelReader<PacketTraceRecord> PeriodicStream => _inner.PeriodicStream;
        public ChannelReader<PacketTraceRecord> CommandStream => _inner.CommandStream;
        public ChannelReader<PacketTraceRecord> AlarmStream => _inner.AlarmStream;

        public void OnPacketTrace(in PacketTraceRecord trace)
        {
            if (trace.Tag == "RESP_RX" && Interlocked.Exchange(ref _reused, 1) == 0)
                Second = Session.RequestAsync<string>("100:REQ2", TimeSpan.FromSeconds(1)).AsTask();
            _inner.OnPacketTrace(trace);
        }
    }
}
