namespace Kable.Melsec.Tests;

using System;
using System.Buffers;
using System.Buffers.Binary;
using Kable.Melsec.Codecs;
using Kable.Melsec.Protocol;
using Xunit;

public class MelsecFrameParsingTests
{
    [Fact]
    public void MelsecSlmpCodec_ExceedingMaxFrameSize_RejectsAndDropsOneByte()
    {
        var codec = new MelsecSlmpCodec(maxFrameSize: 50);

        // Frame claiming responseDataLength = 100 (> 50)
        byte[] raw = new byte[115];
        raw[0] = 0xD0;
        raw[1] = 0x00;
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(7, 2), 100);

        var seq = new ReadOnlySequence<byte>(raw);
        bool decoded = codec.TryDecode(ref seq, out var frame);

        Assert.False(decoded);
        Assert.Null(frame);
        Assert.Equal(114, seq.Length); // 1 byte dropped for resync
    }

    [Fact]
    public void MelsecSlmpCodec_Valid3EResponse_ParsesLittleEndianFieldsAccurately()
    {
        var codec = new MelsecSlmpCodec();

        // 3E Response Frame:
        // Subheader(0xD0, 0x00) + Net(1) + PC(2) + DestIo(0x03FF) + Station(3) + Len(4: EndCode[2] + Data[2]) + EndCode(0x0000) + Data(0x1234)
        byte[] raw = new byte[13];
        raw[0] = 0xD0;
        raw[1] = 0x00;
        raw[2] = 0x01; // Net
        raw[3] = 0x02; // PC
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(4, 2), 0x03FF);
        raw[6] = 0x03; // Station
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(7, 2), 4); // Len = EndCode(2) + Data(2)
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(9, 2), 0x0000); // EndCode
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(11, 2), 0x1234); // Data

        var seq = new ReadOnlySequence<byte>(raw);
        bool decoded = codec.TryDecode(ref seq, out var frame);

        Assert.True(decoded);
        Assert.Equal(0x00D0, frame.Subheader);
        Assert.Equal(1, frame.NetworkNo);
        Assert.Equal(2, frame.PcNo);
        Assert.Equal(0x03FF, frame.DestModuleIo);
        Assert.Equal(3, frame.DestModuleStation);
        Assert.Equal(0, frame.MonitoringTimer);
        Assert.Equal(2, frame.Data.Length);
        Assert.Equal(0x1234, BinaryPrimitives.ReadUInt16LittleEndian(frame.Data.Span));
    }
}
