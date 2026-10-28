namespace Kable.Modbus.Tests.Codecs;

using System.Buffers;
using System.Buffers.Binary;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Xunit;

/// <summary>
/// MBAP Length already covers both request and response. These tests lock that
/// dual-role behavior so a SimPort slave can reuse ModbusTcpCodec unchanged.
/// </summary>
public sealed class ModbusTcpCodecDualRoleTests
{
    [Fact]
    public void EncodeThenTryDecode_Fc03Request_PreservesAddressAndQuantity()
    {
        var codec = new ModbusTcpCodec();
        var writer = new ArrayBufferWriter<byte>();
        var request = ModbusTcpMessage.CreateReadRegisters(0x0010, 0x01, 0x03, startAddress: 100, quantity: 2);

        codec.Encode(request, writer);

        var seq = new ReadOnlySequence<byte>(writer.WrittenMemory);
        Assert.True(codec.TryDecode(ref seq, out var parsed));
        Assert.Equal(0, seq.Length);
        Assert.Equal(0x0010, parsed.TransactionId);
        Assert.Equal(0x03, parsed.FunctionCode);
        Assert.Equal(4, parsed.Payload.Length);
        Assert.Equal(100, BinaryPrimitives.ReadUInt16BigEndian(parsed.Payload.Span.Slice(0, 2)));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(parsed.Payload.Span.Slice(2, 2)));
    }

    [Fact]
    public void EncodeThenTryDecode_Fc03Response_PreservesByteCountAndRegisters()
    {
        var codec = new ModbusTcpCodec();
        var writer = new ArrayBufferWriter<byte>();
        byte[] payload = new byte[5];
        payload[0] = 4;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(1, 2), 0x1234);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(3, 2), 0x5678);
        var response = new ModbusTcpMessage(0x0010, 0x01, 0x03, payload);

        codec.Encode(response, writer);

        var seq = new ReadOnlySequence<byte>(writer.WrittenMemory);
        Assert.True(codec.TryDecode(ref seq, out var parsed));
        Assert.Equal(0, seq.Length);
        Assert.Equal(0x03, parsed.FunctionCode);
        Assert.Equal(5, parsed.Payload.Length);
        Assert.Equal(4, parsed.Payload.Span[0]);
        Assert.Equal(0x1234, BinaryPrimitives.ReadUInt16BigEndian(parsed.Payload.Span.Slice(1, 2)));
        Assert.Equal(0x5678, BinaryPrimitives.ReadUInt16BigEndian(parsed.Payload.Span.Slice(3, 2)));
    }

    [Fact]
    public void EncodeThenTryDecode_Fc16Request_PreservesWritePdu()
    {
        var codec = new ModbusTcpCodec();
        var writer = new ArrayBufferWriter<byte>();
        ushort[] values = [0xAAAA, 0xBBBB];
        var request = ModbusTcpMessage.CreateWriteMultipleRegisters(0x0020, 0x01, 200, values);

        codec.Encode(request, writer);

        var seq = new ReadOnlySequence<byte>(writer.WrittenMemory);
        Assert.True(codec.TryDecode(ref seq, out var parsed));
        Assert.Equal(0x10, parsed.FunctionCode);
        Assert.Equal(9, parsed.Payload.Length);
        Assert.Equal(200, BinaryPrimitives.ReadUInt16BigEndian(parsed.Payload.Span.Slice(0, 2)));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(parsed.Payload.Span.Slice(2, 2)));
        Assert.Equal(4, parsed.Payload.Span[4]);
    }
}
