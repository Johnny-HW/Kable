namespace Kable.Melsec.Tests;

using System;
using System.Buffers;
using System.Threading.Tasks;
using Kable.Engine;
using Kable.Melsec.Codecs;
using Kable.Melsec.Protocol;
using Kable.Transports;
using Xunit;

public sealed class MelsecLengthValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(14)]
    public void MelsecSlmpCodec_InvalidMaxFrameSize_Throws(int maxFrameSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MelsecSlmpCodec(maxFrameSize));
    }

    [Fact]
    public void MelsecSlmpCodec_Encode_OversizedRequestOrResponse_ThrowsInsteadOfTruncating()
    {
        var codec = new MelsecSlmpCodec(maxFrameSize: 131_072);
        var oversizedRequest = new Slmp3EFrame(0x0050, 0, 0xFF, 0x03FF, 0, 0x0010, 0x1401, 0, new byte[ushort.MaxValue]);

        var writer = new ArrayBufferWriter<byte>();
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Encode(oversizedRequest, writer));

        var oversizedResponse = new Slmp3EFrame(0x00D0, 0, 0xFF, 0x03FF, 0, 0, 0, 0, new byte[ushort.MaxValue]);
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Encode(oversizedResponse, writer));
    }

    [Fact]
    public void MelsecSlmpCodec_Encode_ExceedsConfiguredMaxFrameSize_Throws()
    {
        var codec = new MelsecSlmpCodec(maxFrameSize: 64);
        var frame = new Slmp3EFrame(0x0050, 0, 0xFF, 0x03FF, 0, 0x0010, 0x1401, 0, new byte[64]);

        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Encode(frame, new ArrayBufferWriter<byte>()));
    }

    [Fact]
    public async Task MelsecPlcClient_WriteWords_Over16BitFrameLimit_Throws()
    {
        var session = new KableSession<Slmp3EFrame>(new TcpConnectionFactory("127.0.0.1", 5000), new MelsecSlmpCodec());
        await using var client = new MelsecPlcClient(session, new MelsecOptions { Host = "127.0.0.1", Port = 5000 });
        var tooManyWords = new ushort[32_765];

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await client.WriteWordsAsync(MelsecDeviceCode.D, 0, tooManyWords));
    }
}
