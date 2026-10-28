namespace Kable.Tests.Cases.Codecs;

using System;
using System.Buffers;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core.Checksums;
using Xunit;

/// <summary>
/// Dual-role framing: a slave must decode 8-byte FC01-04 requests while a master
/// still decodes ByteCount-prefixed responses. CRC decides the candidate length.
/// </summary>
public sealed class ModbusRtuCodecDualRoleTests
{
    [Theory]
    [InlineData(0x01)]
    [InlineData(0x02)]
    [InlineData(0x03)]
    [InlineData(0x04)]
    public void TryDecode_EightByteReadRequest_ConsumesFullFrame(byte functionCode)
    {
        var codec = new ModbusRtuCodec();
        byte[] frame = CreateFrame(0x01, functionCode, 0x00, 0x10, 0x00, 0x02);

        var seq = new ReadOnlySequence<byte>(frame);
        bool success = codec.TryDecode(ref seq, out var message);

        success.Should().BeTrue();
        message.Length.Should().Be(8);
        message.Span[1].Should().Be(functionCode);
        message.Span[2].Should().Be(0x00);
        message.Span[3].Should().Be(0x10);
        message.Span[4].Should().Be(0x00);
        message.Span[5].Should().Be(0x02);
        seq.Length.Should().Be(0);
    }

    [Fact]
    public void TryDecode_Fc03RequestWithHighAddressByte_DoesNotTreatAddrHiAsByteCount()
    {
        var codec = new ModbusRtuCodec();
        // Addr=0x0400 would look like ByteCount=4 → response length 9 if request discrimination is missing.
        byte[] frame = CreateFrame(0x11, 0x03, 0x04, 0x00, 0x00, 0x0A);

        var seq = new ReadOnlySequence<byte>(frame);
        bool success = codec.TryDecode(ref seq, out var message);

        success.Should().BeTrue();
        message.Length.Should().Be(8);
        seq.Length.Should().Be(0);
    }

    [Fact]
    public void TryDecode_Fc03RequestWithAddrHiAboveMaxFrame_DoesNotDropByte()
    {
        var codec = new ModbusRtuCodec(maxFrameSize: 256);
        // AddrHi=0xFF → naive ByteCount path yields expectedLength=260 > 256 and used to Slice(1).
        byte[] frame = CreateFrame(0x01, 0x03, 0xFF, 0x00, 0x00, 0x01);

        var seq = new ReadOnlySequence<byte>(frame);
        bool success = codec.TryDecode(ref seq, out var message);

        success.Should().BeTrue();
        message.Length.Should().Be(8);
        seq.Length.Should().Be(0);
    }

    [Fact]
    public void TryDecode_IncompleteEightByteRequest_WaitsWithoutConsuming()
    {
        var codec = new ModbusRtuCodec();
        byte[] complete = CreateFrame(0x01, 0x03, 0x00, 0x00, 0x00, 0x02);
        byte[] truncated = complete.AsSpan(0, 7).ToArray();

        var seq = new ReadOnlySequence<byte>(truncated);
        bool success = codec.TryDecode(ref seq, out var message);

        success.Should().BeFalse();
        message.IsEmpty.Should().BeTrue();
        seq.Length.Should().Be(7);
    }

    [Fact]
    public void TryDecode_Fc03ResponseWithTwoRegisters_StillDecodesAsMaster()
    {
        var codec = new ModbusRtuCodec();
        // Slave + FC03 + ByteCount(4) + 2 registers + CRC = 9 bytes
        byte[] payload = [0x01, 0x03, 0x04, 0x12, 0x34, 0x56, 0x78];
        byte[] frame = new byte[payload.Length + 2];
        Crc16Modbus.Append(payload, frame);

        var seq = new ReadOnlySequence<byte>(frame);
        bool success = codec.TryDecode(ref seq, out var message);

        success.Should().BeTrue();
        message.Length.Should().Be(9);
        message.Span[2].Should().Be(0x04);
        seq.Length.Should().Be(0);
    }

    [Fact]
    public void TryDecode_PartialFc03Response_WaitsInsteadOfStealingEightBytePrefix()
    {
        var codec = new ModbusRtuCodec();
        byte[] payload = [0x01, 0x03, 0x04, 0x12, 0x34, 0x56, 0x78];
        byte[] frame = new byte[payload.Length + 2];
        Crc16Modbus.Append(payload, frame);

        var seq = new ReadOnlySequence<byte>(frame.AsMemory(0, 8));
        bool success = codec.TryDecode(ref seq, out var message);

        success.Should().BeFalse();
        message.IsEmpty.Should().BeTrue();
        seq.Length.Should().Be(8);
    }

    [Fact]
    public void TryDecode_Fc02SixByteResponse_StillDecodes()
    {
        var codec = new ModbusRtuCodec();
        byte[] payload = [0x01, 0x02, 0x01, 0x03];
        byte[] frame = new byte[payload.Length + 2];
        Crc16Modbus.Append(payload, frame);

        var seq = new ReadOnlySequence<byte>(frame);
        codec.TryDecode(ref seq, out var message).Should().BeTrue();
        message.Length.Should().Be(6);
        seq.Length.Should().Be(0);
    }

    [Theory]
    [InlineData(0x05)]
    [InlineData(0x06)]
    public void TryDecode_Fc05AndFc06_EightByteRequestAndResponseShareLength(byte functionCode)
    {
        var codec = new ModbusRtuCodec();
        byte[] frame = CreateFrame(0x01, functionCode, 0x00, 0x01, 0xFF, 0x00);

        var seq = new ReadOnlySequence<byte>(frame);
        codec.TryDecode(ref seq, out var message).Should().BeTrue();
        message.Length.Should().Be(8);
        seq.Length.Should().Be(0);
    }

    [Fact]
    public void TryDecode_Fc16WriteMultipleRequest_UsesByteCountNotEightByteAck()
    {
        var codec = new ModbusRtuCodec();
        byte[] payload =
        [
            0x01, 0x10,
            0x00, 0x00,
            0x00, 0x02,
            0x04,
            0x12, 0x34, 0x56, 0x78
        ];
        byte[] frame = new byte[payload.Length + 2];
        Crc16Modbus.Append(payload, frame);

        var seq = new ReadOnlySequence<byte>(frame);
        bool success = codec.TryDecode(ref seq, out var message);

        success.Should().BeTrue();
        message.Length.Should().Be(13);
        message.Span[1].Should().Be(0x10);
        message.Span[6].Should().Be(0x04);
        seq.Length.Should().Be(0);
    }

    [Fact]
    public void TryDecode_Fc16EightByteResponse_StillDecodesAsMasterAck()
    {
        var codec = new ModbusRtuCodec();
        byte[] frame = CreateFrame(0x01, 0x10, 0x00, 0x00, 0x00, 0x02);

        var seq = new ReadOnlySequence<byte>(frame);
        codec.TryDecode(ref seq, out var message).Should().BeTrue();
        message.Length.Should().Be(8);
        seq.Length.Should().Be(0);
    }

    [Fact]
    public void EncodeThenTryDecode_ReadRequestRoundTrips()
    {
        var codec = new ModbusRtuCodec();
        byte[] pdu = [0x01, 0x03, 0x00, 0x00, 0x00, 0x0A];
        var writer = new ArrayBufferWriter<byte>();
        codec.Encode(pdu, writer);

        var seq = new ReadOnlySequence<byte>(writer.WrittenMemory);
        codec.TryDecode(ref seq, out var message).Should().BeTrue();
        message.Length.Should().Be(8);
        message.Span.Slice(0, 6).ToArray().Should().Equal(pdu);
        seq.Length.Should().Be(0);
    }

    private static byte[] CreateFrame(params byte[] payload)
    {
        byte[] frame = new byte[payload.Length + 2];
        Crc16Modbus.Append(payload, frame);
        return frame;
    }
}
