namespace Kable.Melsec.Tests.Codecs;

using System;
using System.Buffers;
using System.Buffers.Binary;
using Kable.Melsec.Codecs;
using Kable.Melsec.Protocol;
using Xunit;

/// <summary>
/// Dual-role SLMP 3E framing: slaves decode 0x0050 requests and encode 0x00D0
/// responses; masters keep encoding requests and decoding EndCode bodies.
/// </summary>
public sealed class MelsecSlmpCodecDualRoleTests
{
    [Fact]
    public void TryDecode_EncodedReadRequest_ExposesCommandSubcommandAndDeviceData()
    {
        var codec = new MelsecSlmpCodec();
        var writer = new ArrayBufferWriter<byte>();
        var request = Slmp3EFrame.CreateReadDeviceRequest(
            device: MelsecDeviceCode.D,
            headDeviceNo: 1000,
            devicePoints: 2);

        codec.Encode(request, writer);

        var seq = new ReadOnlySequence<byte>(writer.WrittenMemory);
        Assert.True(codec.TryDecode(ref seq, out var decoded));
        Assert.Equal(0, seq.Length);

        Assert.True(decoded.IsRequest);
        Assert.False(decoded.IsResponse);
        Assert.Equal(0x0050, decoded.Subheader);
        Assert.Equal(0x0401, decoded.Command);
        Assert.Equal(0x0000, decoded.Subcommand);
        Assert.Equal(16, decoded.MonitoringTimer);
        Assert.Equal(0, decoded.EndCode);

        Assert.Equal(6, decoded.Data.Length);
        int headNo = decoded.Data.Span[0] | (decoded.Data.Span[1] << 8) | (decoded.Data.Span[2] << 16);
        Assert.Equal(1000, headNo);
        Assert.Equal((byte)MelsecDeviceCode.D, decoded.Data.Span[3]);
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(decoded.Data.Span.Slice(4, 2)));
    }

    [Fact]
    public void TryDecode_EncodedWriteRequest_ExposesWritePayload()
    {
        var codec = new MelsecSlmpCodec();
        var writer = new ArrayBufferWriter<byte>();
        ushort[] values = [0x1111, 0x2222];
        var request = Slmp3EFrame.CreateWriteWordsRequest(
            device: MelsecDeviceCode.W,
            headDeviceNo: 50,
            values: values);

        codec.Encode(request, writer);

        var seq = new ReadOnlySequence<byte>(writer.WrittenMemory);
        Assert.True(codec.TryDecode(ref seq, out var decoded));

        Assert.Equal(0x0050, decoded.Subheader);
        Assert.Equal(0x1401, decoded.Command);
        Assert.Equal(0x0000, decoded.Subcommand);
        Assert.Equal(10, decoded.Data.Length);
        Assert.Equal((byte)MelsecDeviceCode.W, decoded.Data.Span[3]);
        Assert.Equal(0x1111, BinaryPrimitives.ReadUInt16LittleEndian(decoded.Data.Span.Slice(6, 2)));
        Assert.Equal(0x2222, BinaryPrimitives.ReadUInt16LittleEndian(decoded.Data.Span.Slice(8, 2)));
    }

    [Fact]
    public void Encode_CreateResponse_WritesD0SubheaderAndEndCodeBody()
    {
        var codec = new MelsecSlmpCodec();
        var writer = new ArrayBufferWriter<byte>();
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), 1234);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), 5678);

        var response = Slmp3EFrame.CreateResponse(endCode: 0, data: payload);
        codec.Encode(response, writer);

        ReadOnlySpan<byte> span = writer.WrittenSpan;
        Assert.Equal(15, span.Length);
        Assert.Equal(0xD0, span[0]);
        Assert.Equal(0x00, span[1]);
        Assert.Equal(0x00, span[2]);
        Assert.Equal(0xFF, span[3]);
        Assert.Equal(0x03FF, BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(4, 2)));
        Assert.Equal(6, BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(7, 2)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(9, 2)));
        Assert.Equal(1234, BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(11, 2)));
        Assert.Equal(5678, BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(13, 2)));
    }

    [Fact]
    public void EncodeThenTryDecode_ErrorResponse_PreservesEndCode()
    {
        var codec = new MelsecSlmpCodec();
        var writer = new ArrayBufferWriter<byte>();
        var response = Slmp3EFrame.CreateResponse(endCode: 0xC050, data: ReadOnlyMemory<byte>.Empty);

        codec.Encode(response, writer);

        var seq = new ReadOnlySequence<byte>(writer.WrittenMemory);
        Assert.True(codec.TryDecode(ref seq, out var decoded));
        Assert.Equal(0, seq.Length);
        Assert.True(decoded.IsResponse);
        Assert.False(decoded.IsRequest);
        Assert.Equal(0x00D0, decoded.Subheader);
        Assert.Equal(0xC050, decoded.EndCode);
        Assert.False(decoded.IsSuccess);
        Assert.Equal(0, decoded.Command);
        Assert.True(decoded.Data.IsEmpty);
    }

    [Fact]
    public void EncodeThenTryDecode_SuccessResponse_RoundTripsData()
    {
        var codec = new MelsecSlmpCodec();
        var writer = new ArrayBufferWriter<byte>();
        byte[] payload = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, 0x1234);
        var response = Slmp3EFrame.CreateResponse(
            endCode: 0,
            data: payload,
            networkNo: 1,
            pcNo: 2,
            destModuleIo: 0x03FF,
            destModuleStation: 3);

        codec.Encode(response, writer);

        var seq = new ReadOnlySequence<byte>(writer.WrittenMemory);
        Assert.True(codec.TryDecode(ref seq, out var decoded));
        Assert.Equal(0x00D0, decoded.Subheader);
        Assert.Equal(1, decoded.NetworkNo);
        Assert.Equal(2, decoded.PcNo);
        Assert.Equal(3, decoded.DestModuleStation);
        Assert.Equal(0, decoded.EndCode);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(0x1234, BinaryPrimitives.ReadUInt16LittleEndian(decoded.Data.Span));
    }

    [Fact]
    public void TryDecode_IncompleteRequest_WaitsWithoutConsuming()
    {
        var codec = new MelsecSlmpCodec();
        var writer = new ArrayBufferWriter<byte>();
        codec.Encode(Slmp3EFrame.CreateReadDeviceRequest(MelsecDeviceCode.D, 0, 1), writer);

        byte[] truncated = writer.WrittenSpan.Slice(0, 12).ToArray();
        var seq = new ReadOnlySequence<byte>(truncated);
        bool decoded = codec.TryDecode(ref seq, out var frame);

        Assert.False(decoded);
        Assert.Null(frame);
        Assert.Equal(12, seq.Length);
    }

    [Fact]
    public void TryDecode_RequestDataLengthBelowCommandHeader_DropsOneByte()
    {
        var codec = new MelsecSlmpCodec();
        byte[] raw = new byte[20];
        raw[0] = 0x50;
        raw[1] = 0x00;
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(7, 2), 4); // < MonitoringTimer+Command+Subcommand (6)

        var seq = new ReadOnlySequence<byte>(raw);
        bool decoded = codec.TryDecode(ref seq, out var frame);

        Assert.False(decoded);
        Assert.Null(frame);
        Assert.Equal(19, seq.Length);
    }
}
