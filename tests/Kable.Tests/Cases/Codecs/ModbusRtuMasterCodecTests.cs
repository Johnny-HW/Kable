namespace Kable.Tests.Cases.Codecs;

using System.Buffers;
using Kable.Core.Checksums;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Xunit;

public sealed class ModbusRtuMasterCodecTests
{
    [Fact]
    public void Encode_UsesKnownCrcAndOmitsMbap()
    {
        var output = new ArrayBufferWriter<byte>();
        new ModbusRtuMasterCodec().Encode(ModbusTcpMessage.CreateReadRegisters(123, 1, 3, 0, 10), output);
        Assert.Equal(new byte[] { 1, 3, 0, 0, 0, 10, 0xC5, 0xCD }, output.WrittenSpan.ToArray());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    public void Decode_FragmentedResponsesConsumeOnlyWhenComplete(byte function, byte count)
    {
        byte[] body = new byte[3 + count];
        body[0] = 1; body[1] = function; body[2] = count;
        byte[] frame = Frame(body);
        var codec = new ModbusRtuMasterCodec();
        for (int i = 0; i < frame.Length; i++)
        {
            var partial = new ReadOnlySequence<byte>(frame.AsMemory(0, i));
            Assert.False(codec.TryDecode(ref partial, out _));
            Assert.Equal(i, partial.Length);
        }
        var input = new ReadOnlySequence<byte>(frame);
        Assert.True(codec.TryDecode(ref input, out var result));
        Assert.Equal(function, result.FunctionCode);
        Assert.Equal(body.AsSpan(2).ToArray(), result.Payload.ToArray());
        Assert.True(input.IsEmpty);
    }

    [Fact]
    public void Decode_TwoFramesAndSegmentedInput()
    {
        byte[] frame = Frame([1, 3, 2, 0x12, 0x34]);
        var first = new Segment(frame.AsMemory(0, 2));
        var last = first.Append(frame.AsMemory(2));
        var segmented = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        var codec = new ModbusRtuMasterCodec();
        Assert.True(codec.TryDecode(ref segmented, out var result));
        Assert.Equal(new byte[] { 2, 0x12, 0x34 }, result.Payload.ToArray());
        var joined = new ReadOnlySequence<byte>(frame.Concat(frame).ToArray());
        Assert.True(codec.TryDecode(ref joined, out _));
        Assert.Equal(frame.Length, joined.Length);
        Assert.True(codec.TryDecode(ref joined, out _));
        Assert.True(joined.IsEmpty);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(15)]
    [InlineData(16)]
    public void Decode_WriteAcknowledgement(byte function)
    {
        var input = new ReadOnlySequence<byte>(Frame([1, function, 0, 1, 0, 2]));
        Assert.True(new ModbusRtuMasterCodec().TryDecode(ref input, out var response));
        Assert.Equal(4, response.Payload.Length);
    }

    [Fact]
    public void Decode_ExceptionResponse()
    {
        var input = new ReadOnlySequence<byte>(Frame([1, 0x83, 2]));
        Assert.True(new ModbusRtuMasterCodec().TryDecode(ref input, out var response));
        Assert.True(response.IsException);
        Assert.Equal(2, response.ExceptionCode);
    }

    [Fact]
    public void Decode_BadCrcAndInvalidHeaderFailClosed()
    {
        byte[] corrupt = Frame([1, 3, 2, 0, 1]);
        corrupt[^1] ^= 0xFF;
        Assert.Throws<InvalidDataException>(() => Decode(corrupt));
        Assert.Throws<InvalidDataException>(() => Decode([1, 3, 255]));
        Assert.Throws<InvalidDataException>(() => Decode([1, 0x7F, 0]));
        Assert.Throws<InvalidDataException>(() => Decode([0, 3, 2]));
    }

    [Fact]
    public void Decode_RejectsWrongUnitOrFunctionForOutstandingRequest()
    {
        var codec = new ModbusRtuMasterCodec();
        codec.Encode(ModbusTcpMessage.CreateReadCoils(1, 1, 0, 1), new ArrayBufferWriter<byte>());
        Assert.Throws<InvalidDataException>(() => DecodeWith(codec, Frame([2, 1, 1, 0])));
        Assert.Throws<InvalidDataException>(() => DecodeWith(codec, Frame([1, 2, 1, 0])));
    }

    private static void Decode(byte[] frame) => DecodeWith(new ModbusRtuMasterCodec(), frame);
    private static void DecodeWith(ModbusRtuMasterCodec codec, byte[] frame)
    {
        var input = new ReadOnlySequence<byte>(frame);
        codec.TryDecode(ref input, out _);
    }
    private static byte[] Frame(byte[] body)
    {
        byte[] frame = new byte[body.Length + 2];
        Crc16Modbus.Append(body, frame);
        return frame;
    }
    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;
        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
