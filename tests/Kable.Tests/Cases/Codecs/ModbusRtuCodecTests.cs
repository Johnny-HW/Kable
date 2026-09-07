namespace Kable.Tests.Cases.Codecs;

using System;
using System.Buffers;
using Kable.Codecs;
using Kable.Core.Checksums;
using Xunit;

public class ModbusRtuCodecTests
{
    [Fact]
    public void Encode_AppendsCrcAutomatically()
    {
        var codec = new ModbusRtuCodec();
        var bufferWriter = new ArrayBufferWriter<byte>();

        // PDU: Slave 1, FC 5, Addr 0x0000, Val 0xFF00
        byte[] pdu = [0x01, 0x05, 0x00, 0x00, 0xFF, 0x00];
        codec.Encode(pdu, bufferWriter);

        var encoded = bufferWriter.WrittenSpan;
        Assert.Equal(8, encoded.Length);
        Assert.True(Crc16Modbus.Validate(encoded));
    }

    [Fact]
    public void TryDecode_ValidFc02Response_DecodesCleanly()
    {
        var codec = new ModbusRtuCodec();

        // Slave 1, FC 2, ByteCount 1, Data 0x03 + CRC
        byte[] payload = [0x01, 0x02, 0x01, 0x03];
        byte[] frame = new byte[payload.Length + 2];
        Crc16Modbus.Append(payload, frame);

        var seq = new ReadOnlySequence<byte>(frame);
        bool success = codec.TryDecode(ref seq, out var message);

        Assert.True(success);
        Assert.Equal(6, message.Length);
        Assert.Equal(0, seq.Length); // 버퍼 소비 완료
    }

    [Fact]
    public void TryDecode_CorruptedCrc_ReturnsFalseAndDropsByte()
    {
        var codec = new ModbusRtuCodec();

        byte[] corruptFrame = [0x01, 0x05, 0x00, 0x00, 0xFF, 0x00, 0x11, 0x22]; // 잘못된 CRC
        var seq = new ReadOnlySequence<byte>(corruptFrame);

        bool success = codec.TryDecode(ref seq, out var message);
        Assert.False(success);
        Assert.Equal(7, seq.Length); // 1바이트 드롭되어 재동기화 시도
    }
}
