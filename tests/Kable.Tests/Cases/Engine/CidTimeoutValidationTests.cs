namespace Kable.Tests.Cases.Engine;

using System.Buffers;
using Kable.Codecs;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class CidTimeoutValidationTests
{
    [Theory]
    [InlineData(-2)]
    [InlineData(-1000)]
    [InlineData(4294967295d)]
    public async Task InvalidTimeout_DoesNotReserveCid_AndSameCidCanSucceed(double milliseconds)
    {
        var factory = new TestMemoryConnectionFactory();
        await using var session = new KableSession<string>(factory, new FixedCidCodec());
        await session.StartAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await session.RequestAsync<string>("REQUEST", TimeSpan.FromMilliseconds(milliseconds)));
        Assert.True(session.IsConnected);
        Assert.False(factory.Context.RemoteRead.TryRead(out _));

        var request = session.RequestAsync<string>("REQUEST", TimeSpan.FromSeconds(5)).AsTask();
        var transmitted = await factory.Context.RemoteRead.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        factory.Context.RemoteRead.AdvanceTo(transmitted.Buffer.End);
        await factory.Context.WriteAsciiLineAsync("RESPONSE");
        Assert.Equal("RESPONSE", await request.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task InfiniteTimeout_StillRejectsDuplicateInFlightCid()
    {
        var factory = new TestMemoryConnectionFactory();
        await using var session = new KableSession<string>(factory, new FixedCidCodec());
        await session.StartAsync();
        var request = session.RequestAsync<string>("REQUEST", Timeout.InfiniteTimeSpan).AsTask();
        var transmitted = await factory.Context.RemoteRead.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        factory.Context.RemoteRead.AdvanceTo(transmitted.Buffer.End);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.RequestAsync<string>("DUPLICATE", TimeSpan.FromSeconds(5)));
        await factory.Context.WriteAsciiLineAsync("RESPONSE");
        Assert.Equal("RESPONSE", await request.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private sealed class FixedCidCodec : IProtocolCodec<string>
    {
        private readonly AsciiLineCodec _inner = new(delimiter: 0x0A);
        public bool SupportsCorrelationId => true;
        public int MaxFrameSize => _inner.MaxFrameSize;
        public void Encode(string message, IBufferWriter<byte> output) => _inner.Encode(message, output);
        public bool TryDecode(ref ReadOnlySequence<byte> buffer, out string message) => _inner.TryDecode(ref buffer, out message);
        public string ExtractCorrelationId(string message) => "same-cid";
        public bool IsAutonomousMessage(string message) => false;
    }
}
