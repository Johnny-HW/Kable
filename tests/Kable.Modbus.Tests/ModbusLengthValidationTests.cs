namespace Kable.Modbus.Tests;

using System;
using System.Buffers;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Xunit;

public sealed class ModbusLengthValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(7)]
    public void ModbusTcpCodec_InvalidMaxFrameSize_Throws(int maxFrameSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModbusTcpCodec(maxFrameSize));
    }

    [Fact]
    public void ModbusTcpCodec_Encode_ExceedsMaxFrameSizeOrUInt16_ThrowsInsteadOfTruncating()
    {
        var defaultCodec = new ModbusTcpCodec(maxFrameSize: 260);
        var exceedsMax = new ModbusTcpMessage(1, 1, 3, new byte[260]);
        Assert.Throws<ArgumentOutOfRangeException>(() => defaultCodec.Encode(exceedsMax, new ArrayBufferWriter<byte>()));

        var largeCodec = new ModbusTcpCodec(maxFrameSize: 131_072);
        var exceedsUInt16 = new ModbusTcpMessage(1, 1, 3, new byte[ushort.MaxValue]);
        Assert.Throws<ArgumentOutOfRangeException>(() => largeCodec.Encode(exceedsUInt16, new ArrayBufferWriter<byte>()));
    }
}
