namespace Kable.Tests.Cases.Codecs;

using System;
using System.Buffers;
using Kable.Codecs;
using Kable.Core.Checksums;
using Kable.Tests.Infrastructure;
using Xunit;

public sealed class ModbusRtuReadCandidateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoiseBeforeSixByteResponse_RejectsImpossibleRequestPrefix(bool segmented)
    {
        var codec = new ModbusRtuCodec();
        byte[] frame = Frame(1, 2, 1, 3);
        byte[] input = new byte[7];
        input[0] = 0x9B;
        frame.CopyTo(input, 1);
        var buffer = Sequence(input, segmented);

        Assert.False(codec.TryDecode(ref buffer, out _));
        Assert.Equal(6, buffer.Length);
        Assert.True(codec.TryDecode(ref buffer, out var message));
        Assert.Equal(frame, message.ToArray());
        Assert.True(buffer.IsEmpty);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void IncompleteReadRequests_PreserveAllPrefixesIncludingHighAddresses(byte function)
    {
        foreach (byte addressHigh in new byte[] { 0, 4, 255 })
        foreach (bool segmented in new[] { false, true })
        {
            var codec = new ModbusRtuCodec();
            byte[] frame = Frame(1, function, addressHigh, 0, 0, 2);
            for (int length = 4; length < frame.Length; length++)
            {
                var buffer = Sequence(frame.AsSpan(0, length).ToArray(), segmented);
                Assert.False(codec.TryDecode(ref buffer, out var message));
                Assert.True(message.IsEmpty);
                Assert.Equal(length, buffer.Length);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompleteResponseWithTrailingByte_IsDecodedBeforeRequestPrefixCheck(bool segmented)
    {
        var codec = new ModbusRtuCodec();
        byte[] frame = Frame(1, 2, 1, 3);
        var bytes = new byte[7];
        frame.CopyTo(bytes, 0);
        bytes[6] = 0xAA;
        var buffer = Sequence(bytes, segmented);
        Assert.True(codec.TryDecode(ref buffer, out var message));
        Assert.Equal(frame, message.ToArray());
        Assert.Equal(1, buffer.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongerResponse_PreservesIncompleteSevenBytePrefix(bool segmented)
    {
        var codec = new ModbusRtuCodec();
        byte[] frame = Frame(1, 3, 4, 0x12, 0x34, 0x56, 0x78);
        var buffer = Sequence(frame.AsSpan(0, 7).ToArray(), segmented);
        Assert.False(codec.TryDecode(ref buffer, out _));
        Assert.Equal(7, buffer.Length);
        var complete = Sequence(frame, segmented);
        Assert.True(codec.TryDecode(ref complete, out var decoded));
        Assert.Equal(frame, decoded.ToArray());
    }

    [Fact]
    public void SevenByteRequestPrefixValidation_AllocatesNothing()
    {
        var codec = new ModbusRtuCodec();
        byte[] prefix = Frame(1, 3, 0, 0, 0, 2).AsSpan(0, 7).ToArray();
        AllocationAssert.AssertZeroAllocation(() =>
        {
            var buffer = new ReadOnlySequence<byte>(prefix);
            codec.TryDecode(ref buffer, out _);
        }, iterations: 1000);
    }

    private static byte[] Frame(params byte[] payload)
    {
        var result = new byte[payload.Length + 2];
        Crc16Modbus.Append(payload, result);
        return result;
    }

    private static ReadOnlySequence<byte> Sequence(byte[] bytes, bool segmented)
    {
        if (!segmented) return new ReadOnlySequence<byte>(bytes);
        var first = new Segment(bytes.AsMemory(0, 3));
        var last = first.Append(bytes.AsMemory(3));
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) { Memory = memory; }
        public Segment Append(ReadOnlyMemory<byte> nextMemory)
        {
            var next = new Segment(nextMemory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
