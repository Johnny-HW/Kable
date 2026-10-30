namespace Kable.Tests.Cases.Codecs;

using System.Buffers;
using Kable.Codecs;
using Kable.Exceptions;
using Xunit;

public sealed class FramingAuditRegressionTests
{
    [Fact]
    public void SameMarkerDecodesPayloadAndPreservesNextFrame()
    {
        var codec = new BytesCodec(new() { StartMarker = 0x7E, EndDelimiter = [0x7E] });
        var sequence = new ReadOnlySequence<byte>(new byte[] { 0x7E, 1, 2, 0x7E, 0x7E, 3, 0x7E });
        Assert.True(codec.TryDecode(ref sequence, out var first));
        Assert.Equal(new byte[] { 1, 2 }, first);
        Assert.True(codec.TryDecode(ref sequence, out var second));
        Assert.Equal(new byte[] { 3 }, second);
    }

    [Fact]
    public void OversizeDiagnosticPreservesOriginalLength()
    {
        var codec = new BytesCodec(new() { MaxFrameSize = 4 });
        var sequence = new ReadOnlySequence<byte>(new byte[] { 1, 2, 3, 4, 5 });
        var error = Assert.Throws<ProtocolViolationException>(() => codec.TryDecode(ref sequence, out _));
        Assert.Contains("5 > 4", error.Message);
        Assert.True(sequence.IsEmpty);
    }

    [Fact]
    public void MissingLengthFieldMarkerReportsRejectedLength()
    {
        var codec = new LengthBytesCodec();
        var sequence = new ReadOnlySequence<byte>(new byte[] { 1, 2, 3, 4, 5 });
        var error = Assert.Throws<ProtocolViolationException>(() => codec.TryDecode(ref sequence, out _));
        Assert.Contains("5 > 4", error.Message);
        Assert.True(sequence.IsEmpty);
    }

    private sealed class LengthBytesCodec() : LengthFieldCodec<byte[]>(new()
    {
        HeaderMarker = 0x7E, MaxFrameSize = 4
    })
    {
        protected override bool TryDecodePayload(in ReadOnlySequence<byte> sequence, out byte[] message)
        {
            message = sequence.ToArray();
            return true;
        }
        public override void Encode(byte[] message, IBufferWriter<byte> output) => throw new NotSupportedException();
    }

    private sealed class BytesCodec(DelimitedFrameOptions options) : DelimitedFrameCodec<byte[]>(options)
    {
        protected override bool TryDecodePayload(in ReadOnlySequence<byte> sequence, out byte[] message)
        {
            message = sequence.ToArray();
            return true;
        }
        public override void Encode(byte[] message, IBufferWriter<byte> output) => throw new NotSupportedException();
    }
}
