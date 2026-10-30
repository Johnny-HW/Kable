namespace Kable.Melsec.Tests.Codecs;

using System.Buffers;
using Kable.Engine;
using Kable.Melsec.Codecs;
using Kable.Melsec.Protocol;
using Xunit;

public sealed class ResponseValidationTests
{
    [Fact]
    public void UnknownSubheaderIsNotDecodedOrSuccessful()
    {
        var frame = new Slmp3EFrame(0xFFFF, 0, 255, 1023, 0, 0, 0, 0, default);
        Assert.False(frame.IsSuccess);
        var bytes = new ReadOnlySequence<byte>(new byte[] { 255, 255, 0, 255, 255, 3, 0, 2, 0, 0, 0 });
        Assert.False(new MelsecSlmpCodec().TryDecode(ref bytes, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    public async Task ReadRejectsUnexpectedPayloadLength(int length)
    {
        var factory = new MockPlcConnectionFactory();
        await using var session = new KableSession<Slmp3EFrame>(factory, new MelsecSlmpCodec());
        await session.StartAsync();
        var client = new MelsecPlcClient(session, new MelsecOptions());
        var request = client.ReadWordsAsync(MelsecDeviceCode.D, 0, 2);
        var read = await factory.Connection.ServerReader.ReadAsync();
        factory.Connection.ServerReader.AdvanceTo(read.Buffer.End);
        var writer = new ArrayBufferWriter<byte>();
        new MelsecSlmpCodec().Encode(Slmp3EFrame.CreateResponse(0, new byte[length]), writer);
        await factory.Connection.ServerWriter.WriteAsync(writer.WrittenMemory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request);
    }
}
