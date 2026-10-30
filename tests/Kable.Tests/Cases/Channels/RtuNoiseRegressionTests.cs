namespace Kable.Tests.Cases.Channels;

using System.Buffers;
using Kable.Channels;
using Kable.Codecs;
using Kable.Core;
using Kable.Transports;
using Xunit;

public sealed class RtuNoiseRegressionTests
{
    [Fact]
    public async Task NoiseAndResponseInSameReadDoesNotRequireAnotherWrite()
    {
        var (connection, peer) = InMemoryConnectionContext.CreatePair();
        await using var peerScope = peer;
        await using var channel = new KableModbusDeviceChannel(new Factory(connection));
        await channel.OpenAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        byte[] pdu = [1, 6, 0, 1, 0, 42];
        var request = channel.SendAndReceiveFrameAsync(pdu, TimeSpan.FromSeconds(2), deadline.Token).AsTask();
        var read = await peer.Input.ReadAsync(deadline.Token);
        peer.Input.AdvanceTo(read.Buffer.End);
        var writer = new ArrayBufferWriter<byte>();
        new ModbusRtuCodec().Encode(pdu, writer);
        await peer.Output.WriteAsync(new byte[] { 255 }.Concat(writer.WrittenMemory.ToArray()).ToArray(), deadline.Token);
        Assert.Equal(writer.WrittenMemory.ToArray(), await request);
    }
    private sealed class Factory(IConnectionContext context) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(context);
    }
}
