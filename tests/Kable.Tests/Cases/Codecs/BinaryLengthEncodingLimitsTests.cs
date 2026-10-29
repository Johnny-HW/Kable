namespace Kable.Tests.Cases.Codecs;

using System.Buffers;
using Kable.Codecs;
using Xunit;

public class BinaryLengthEncodingLimitsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoByteHeader_RoundTripsMaximumRepresentableLength(bool bigEndian)
    {
        var codec = new BinaryLengthPrefixedCodec(2, bigEndian);
        var writer = new ArrayBufferWriter<byte>();
        codec.Encode(new byte[65535], writer);
        var input = new ReadOnlySequence<byte>(writer.WrittenMemory);
        Assert.True(codec.TryDecode(ref input, out var decoded));
        Assert.Equal(65535, decoded.Length);
        Assert.True(input.IsEmpty);
    }

    [Theory]
    [InlineData(2, 65536, 65536)]
    [InlineData(4, 50, 51)]
    public void OversizedPayload_IsRejectedBeforeWriting(int header, int limit, int size)
    {
        var codec = new BinaryLengthPrefixedCodec(header, maxFrameSize: limit);
        var writer = new ArrayBufferWriter<byte>();
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Encode(new byte[size], writer));
        Assert.Equal(0, writer.WrittenCount);
    }
}
