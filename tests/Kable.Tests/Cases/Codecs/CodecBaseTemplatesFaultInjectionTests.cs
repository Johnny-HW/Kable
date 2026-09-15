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

    [Fact]
    public void DelimitedFrameCodec_ExactMaxFrameSize_PayloadAllowed()
    {
        // MaxFrameSize is 10. Payload is exactly 10 bytes, plus 1 byte delimiter (total frame = 11).
        // Under payload-based limit, payload length 10 <= 10 is allowed!
        var options = new DelimitedFrameOptions
        {
            EndDelimiter = new byte[] { 0x0A },
            StripDelimiters = true,
            MaxFrameSize = 10
        };
        var codec = new TestDelimitedCodec(options);

        byte[] raw = Encoding.ASCII.GetBytes("1234567890\n"); // 10 bytes + delimiter
        var seq = new ReadOnlySequence<byte>(raw);

        bool success = codec.TryDecode(ref seq, out var msg);
        success.Should().BeTrue();
        msg.Should().Be("1234567890");
        seq.Length.Should().Be(0);
    }

    [Fact]
    public void LengthFieldCodec_LengthIncludesHeaderAndTrailer_CalculatesCorrectly()
    {
        // HeaderMarker = 0xAA (1B), LengthFieldOffset = 1, LengthFieldLength = 2 (BigEndian)
        // LengthIncludesHeader = true, TrailerLength = 1 (Checksum byte 0x55)
        var options = new LengthFieldOptions
        {
            HeaderMarker = 0xAA,
            LengthFieldOffset = 1,
            LengthFieldLength = 2,
            IsBigEndian = true,
            LengthIncludesHeader = true,
            TrailerLength = 1,
            MaxFrameSize = 1024
        };
        var codec = new TestLengthFieldCodec(options);

        // Header(1B) + Length(2B) = 3B min header.
        // Payload = "TEST" (4B).
        // Total raw length recorded in length field = 3(header) + 4(payload) = 7.
        // Trailer = 0x55 (1B).
        // Total frame size = 7 + 1 = 8 bytes.
        byte[] raw = new byte[] { 0xAA, 0x00, 0x07, (byte)'T', (byte)'E', (byte)'S', (byte)'T', 0x55 };
        var seq = new ReadOnlySequence<byte>(raw);

        bool success = codec.TryDecode(ref seq, out var msg);
        success.Should().BeTrue();
        msg.Should().Be("TEST");
        seq.Length.Should().Be(0);
    }

    private sealed class ChecksumValidatingCodec : LengthFieldCodec<string>
    {
        public ChecksumValidatingCodec(LengthFieldOptions options) : base(options) { }

        protected override bool TryDecodePayload(in ReadOnlySequence<byte> frameSequence, out string message)
        {
            // Simple XOR checksum of payload matching last byte
            byte[] frameBytes = frameSequence.ToArray();
            int headerLen = Options.LengthFieldOffset + Options.LengthFieldLength;
            int payloadLen = frameBytes.Length - headerLen - Options.TrailerLength;

            byte xor = 0;
            for (int i = headerLen; i < headerLen + payloadLen; i++)
            {
                xor ^= frameBytes[i];
            }

            byte expectedChecksum = frameBytes[frameBytes.Length - 1];
            if (xor != expectedChecksum)
            {
                message = string.Empty;
                return false; // Checksum mismatch
            }

            message = Encoding.ASCII.GetString(frameBytes, headerLen, payloadLen);
            return true;
        }

        public override void Encode(string message, IBufferWriter<byte> output) => throw new NotImplementedException();
    }

    [Fact]
    public void LengthFieldCodec_ChecksumFailure_ResynchronizesToNextValidHeader()
    {
        var options = new LengthFieldOptions
        {
            HeaderMarker = 0x02,
            LengthFieldOffset = 1,
            LengthFieldLength = 1,
            IsBigEndian = true,
            LengthIncludesHeader = false,
            TrailerLength = 1,
            ResynchronizeOnInvalidHeader = true,
            MaxFrameSize = 1024
        };
        var codec = new ChecksumValidatingCodec(options);

        // Frame 1: STX(0x02) + Length(3) + 'X' + 'Y' + 'Z' + Bad Checksum(0x00)
        // Frame 2: STX(0x02) + Length(2) + 'C' + 'D' + Valid Checksum('C'^'D')
        byte validXor = (byte)('C' ^ 'D');
        byte[] corruptedAndValid = new byte[]
        {
            0x02, 0x03, (byte)'X', (byte)'Y', (byte)'Z', 0x00, // Corrupted frame (bad checksum 0x00)
            0x02, 0x02, (byte)'C', (byte)'D', validXor // Valid frame
        };
        var seq = new ReadOnlySequence<byte>(corruptedAndValid);

        bool success = codec.TryDecode(ref seq, out var msg);
        success.Should().BeTrue();
        msg.Should().Be("CD");
        seq.Length.Should().Be(0);
    }

    private sealed class CustomBufferSegment : ReadOnlySequenceSegment<byte>
    {
        public CustomBufferSegment(ReadOnlyMemory<byte> memory) => Memory = memory;

        public CustomBufferSegment SetNext(CustomBufferSegment next)
        {
            Next = next;
            next.RunningIndex = RunningIndex + Memory.Length;
            return next;
        }
    }

    [Fact]
    public void DelimitedFrameCodec_MultiSegmentInput_DecodesAcrossChunks()
    {
        var options = new DelimitedFrameOptions
        {
            EndDelimiter = new byte[] { 0x0D, 0x0A },
            StripDelimiters = true,
            MaxFrameSize = 1024
        };
        var codec = new TestDelimitedCodec(options);

        var seg1 = new CustomBufferSegment(Encoding.ASCII.GetBytes("SEGMENT1_"));
        var seg2 = new CustomBufferSegment(Encoding.ASCII.GetBytes("SEGMENT2_\r"));
        var seg3 = new CustomBufferSegment(Encoding.ASCII.GetBytes("\n"));
        seg1.SetNext(seg2).SetNext(seg3);

        var seq = new ReadOnlySequence<byte>(seg1, 0, seg3, seg3.Memory.Length);

        bool success = codec.TryDecode(ref seq, out var msg);
        success.Should().BeTrue();
        msg.Should().Be("SEGMENT1_SEGMENT2_");
        seq.Length.Should().Be(0);
    }
}
