namespace Kable.Tests.Cases.Engine.Decoding;

using System.Buffers;
using Kable.Codecs;
using Kable.Engine;
using Kable.Modbus;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class DecoderProgressTests
{
    [Fact]
    public void SuccessfulDecoderWithoutConsumptionIsRejected()
    {
        var decoder = new Kable.Engine.Decoding.FrameDecoder<string>(new NoProgressCodec());
        Assert.Throws<Kable.Exceptions.ProtocolViolationException>(() =>
        {
            var bytes = new ReadOnlySequence<byte>(new byte[] { 1 });
            decoder.TryRead(ref bytes, out _);
        });
    }

    [Fact]
    public async Task GarbageAndCompleteReplyInSameReadNeedsNoAdditionalBytes()
    {
        var link = new DesignLink();
        var codec = new ModbusTcpCodec();
        await using var session = new KableSession<ModbusTcpMessage>(link, codec);
        await using var master = new ModbusTcpMaster(session, new ModbusTcpOptions { Timeout = TimeSpan.FromSeconds(1) });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await master.StartAsync(deadline.Token);
        var pending = master.ReadHoldingRegistersAsync(0, 1, ct: deadline.Token);
        var read = await link.Peer.Input.ReadAsync(deadline.Token);
        var input = read.Buffer;
        Assert.True(codec.TryDecode(ref input, out var request));
        link.Peer.Input.AdvanceTo(read.Buffer.End);
        var output = new ArrayBufferWriter<byte>();
        codec.Encode(new(request.TransactionId, request.UnitId, 3, new byte[] { 2, 0, 7 }), output);
        byte[] combined = new byte[6 + output.WrittenCount]; output.WrittenSpan.CopyTo(combined.AsSpan(6));
        await link.Peer.Output.WriteAsync(combined, deadline.Token);
        Assert.Equal(new ushort[] { 7 }, await pending);
    }

    [Fact]
    public async Task PartialFrameWaitsForMoreInput()
    {
        var link = new DesignLink();
        await using var session = new KableSession<string>(link, new AsciiLineCodec());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await session.StartAsync(deadline.Token);
        var pending = session.RequestAsync<string>("Q", TimeSpan.FromSeconds(1), deadline.Token).AsTask();
        await link.ReadRequestAsync(deadline.Token); await link.SendAsync("REA", deadline.Token);
        Assert.False(pending.IsCompleted);
        await link.SendAsync("DY\n", deadline.Token);
        Assert.Equal("READY", await pending);
    }

    private sealed class NoProgressCodec : IProtocolCodec<string>
    {
        public bool SupportsCorrelationId => false;
        public bool TryDecode(ref ReadOnlySequence<byte> input, out string message) { message = "bad"; return true; }
        public void Encode(string message, IBufferWriter<byte> output) => throw new NotSupportedException();
        public string? ExtractCorrelationId(string message) => null;
        public bool IsAutonomousMessage(string message) => false;
    }
}
