namespace Kable.Modbus.Tests;

using System;
using System.Buffers;
using System.Buffers.Binary;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Xunit;

public class ModbusEndianAndAllocationTests
{
    [Fact]
    public void ModbusTcpCodec_InvalidHeaderLength_RejectsAndDropsOneByte()
    {
        var codec = new ModbusTcpCodec(maxFrameSize: 260);

        // Frame with Length field = 300 (> maxFrameSize 260)
        byte[] raw = new byte[310];
        BinaryPrimitives.WriteUInt16BigEndian(raw.AsSpan(0, 2), 1); // TransId
        BinaryPrimitives.WriteUInt16BigEndian(raw.AsSpan(2, 2), 0); // ProtoId
        BinaryPrimitives.WriteUInt16BigEndian(raw.AsSpan(4, 2), 300); // Length > 260

        var seq = new ReadOnlySequence<byte>(raw);
        bool decoded = codec.TryDecode(ref seq, out var msg);

        Assert.False(decoded);
        Assert.Null(msg);
        Assert.Equal(309, seq.Length); // 1 byte dropped for resync
    }

    [Fact]
    public void ModbusTcpCodec_BigEndianParsing_MatchesHostByteOrderIndependence()
    {
        var codec = new ModbusTcpCodec();

        byte[] raw = [
            0x12, 0x34, // TransId: 0x1234 (4660)
            0x00, 0x00, // ProtoId: 0
            0x00, 0x06, // Length: 6 (UnitId + FC + 4 bytes payload)
            0x01,       // UnitId: 1
            0x03,       // FC: 03
            0xAA, 0xBB, 0xCC, 0xDD // 4 bytes payload
        ];

        var seq = new ReadOnlySequence<byte>(raw);
        bool decoded = codec.TryDecode(ref seq, out var msg);

        Assert.True(decoded);
        Assert.Equal(0x1234, msg.TransactionId);
        Assert.Equal(1, msg.UnitId);
        Assert.Equal(3, msg.FunctionCode);

        // Verify payload endianness
        ushort val1 = BinaryPrimitives.ReadUInt16BigEndian(msg.Payload.Span.Slice(0, 2));
        ushort val2 = BinaryPrimitives.ReadUInt16BigEndian(msg.Payload.Span.Slice(2, 2));
        Assert.Equal(0xAABB, val1);
        Assert.Equal(0xCCDD, val2);
    }
}
