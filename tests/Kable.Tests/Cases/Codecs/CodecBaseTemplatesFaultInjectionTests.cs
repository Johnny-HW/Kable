namespace Kable.Tests.Cases.Codecs;

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using Kable.Codecs;
using Kable.Exceptions;
using Xunit;

public class CodecBaseTemplatesFaultInjectionTests
{
    private sealed class TestLengthFieldCodec : LengthFieldCodec<string>
    {
        public TestLengthFieldCodec(LengthFieldOptions options) : base(options) { }

        protected override bool TryDecodePayload(in ReadOnlySequence<byte> frameSequence, out string message)
        {
            int headerLength = Options.LengthFieldOffset + Options.LengthFieldLength;
            var payloadSeq = frameSequence.Slice(headerLength, frameSequence.Length - headerLength - Options.TrailerLength);
            byte[] bytes = payloadSeq.ToArray();
            message = Encoding.ASCII.GetString(bytes);
            return true;
        }

        public override void Encode(string message, IBufferWriter<byte> output) => throw new NotImplementedException();
    }

    private sealed class TestDelimitedCodec : DelimitedFrameCodec<string>
    {
        public TestDelimitedCodec(DelimitedFrameOptions options) : base(options) { }

        protected override bool TryDecodePayload(in ReadOnlySequence<byte> payloadSequence, out string message)
        {
            byte[] bytes = payloadSequence.ToArray();
            message = Encoding.ASCII.GetString(bytes);
            return true;
        }

        public override void Encode(string message, IBufferWriter<byte> output) => throw new NotImplementedException();
    }

    [Fact]
    public void LengthFieldCodec_WithGarbagePrefix_ResynchronizesToHeaderMarker()
    {
        // HeaderMarker = 0x02 (STX), LengthFieldOffset = 1, LengthFieldLength = 2 (BigEndian, IncludesHeader: false)
        var options = new LengthFieldOptions
        {
            HeaderMarker = 0x02,
            LengthFieldOffset = 1,
            LengthFieldLength = 2,
            IsBigEndian = true,
            LengthIncludesHeader = false,
            ResynchronizeOnInvalidHeader = true,
            MaxFrameSize = 1024
        };
        var codec = new TestLengthFieldCodec(options);

        // Noise (3 bytes: 0xFF, 0xEE, 0xDD) + STX(0x02) + Length(ushort 4 = "DATA") + "DATA"
        byte[] raw = new byte[] { 0xFF, 0xEE, 0xDD, 0x02, 0x00, 0x04, (byte)'D', (byte)'A', (byte)'T', (byte)'A' };
        var seq = new ReadOnlySequence<byte>(raw);

        bool success = codec.TryDecode(ref seq, out var msg);

        success.Should().BeTrue();
        msg.Should().Be("DATA");
        seq.Length.Should().Be(0);
    }

    [Fact]
    public void LengthFieldCodec_WithGarbagePrefix_ThrowsWhenResyncDisabled()
    {
        var options = new LengthFieldOptions
        {
            HeaderMarker = 0x02,
            LengthFieldOffset = 1,
            LengthFieldLength = 2,
            ResynchronizeOnInvalidHeader = false,
            MaxFrameSize = 1024
        };
        var codec = new TestLengthFieldCodec(options);

        byte[] raw = new byte[] { 0xFF, 0x02, 0x00, 0x04, (byte)'D', (byte)'A', (byte)'T', (byte)'A' };
        var seq = new ReadOnlySequence<byte>(raw);

        Action act = () => codec.TryDecode(ref seq, out _);
        act.Should().Throw<ProtocolViolationException>()
           .WithMessage("*Invalid leading bytes*");
    }

    [Fact]
    public void DelimitedFrameCodec_MultiByteDelimiter_HandlesCRLF()
    {
        var options = new DelimitedFrameOptions
        {
            EndDelimiter = new byte[] { 0x0D, 0x0A }, // \r\n
            StripDelimiters = true,
            MaxFrameSize = 1024
        };
        var codec = new TestDelimitedCodec(options);

        byte[] raw = Encoding.ASCII.GetBytes("HELLO WORLD\r\nNEXT");
        var seq = new ReadOnlySequence<byte>(raw);

        bool success = codec.TryDecode(ref seq, out var msg);

        success.Should().BeTrue();
        msg.Should().Be("HELLO WORLD");
        seq.Length.Should().Be(4); // "NEXT" remains
    }

    [Fact]
    public void DelimitedFrameCodec_WithStartMarkerAndGarbage_ResynchronizesCleanly()
    {
        // Start: 0x02 (STX), End: 0x03 (ETX)
        var options = new DelimitedFrameOptions
        {
            StartMarker = 0x02,
            EndDelimiter = new byte[] { 0x03 },
            StripDelimiters = true,
            ResynchronizeOnGarbage = true,
            MaxFrameSize = 1024
        };
        var codec = new TestDelimitedCodec(options);

        // Noise bytes + STX + "VALID" + ETX
        byte[] raw = new byte[] { 0xAA, 0xBB, 0xCC, 0x02, (byte)'V', (byte)'A', (byte)'L', (byte)'I', (byte)'D', 0x03 };
        var seq = new ReadOnlySequence<byte>(raw);

        bool success = codec.TryDecode(ref seq, out var msg);

        success.Should().BeTrue();
        msg.Should().Be("VALID");
        seq.Length.Should().Be(0);
    }

    [Fact]
    public void DelimitedFrameCodec_ExceedingMaxFrameSizeWithoutDelimiter_ThrowsOomDefense()
    {
        var options = new DelimitedFrameOptions
        {
            EndDelimiter = new byte[] { 0x0A },
            MaxFrameSize = 16,
            ResynchronizeOnGarbage = false
        };
        var codec = new TestDelimitedCodec(options);

        byte[] raw = Encoding.ASCII.GetBytes("THIS_IS_A_VERY_LONG_LINE_WITHOUT_DELIMITER");
        var seq = new ReadOnlySequence<byte>(raw);

        Action act = () => codec.TryDecode(ref seq, out _);
        act.Should().Throw<ProtocolViolationException>()
           .WithMessage("*Frame size limit exceeded*");
    }
}
