namespace Kable.Modbus.Tests.Data;

using Kable.Modbus.Data;
using Xunit;

public sealed class PlcStringConverterTests
{
    [Fact]
    public void AsciiString_IsBigEndianAndZeroPadded()
    {
        ushort[] target = [0xFFFF, 0xFFFF, 0xFFFF];
        PlcRegisterConverter.WriteString("ABC", target);
        Assert.Equal(new ushort[] { 0x4142, 0x4300, 0 }, target);
        Assert.Equal("ABC", PlcRegisterConverter.ReadString(target));
        Assert.Equal("ABC\0\0\0", PlcRegisterConverter.ReadString(target, trimNullPadding: false));
    }

    [Theory]
    [InlineData(PlcDataOrder.BigEndian, 0x4142, 0x4344)]
    [InlineData(PlcDataOrder.ByteSwap, 0x4241, 0x4443)]
    [InlineData(PlcDataOrder.WordSwap, 0x4344, 0x4142)]
    [InlineData(PlcDataOrder.LittleEndian, 0x4443, 0x4241)]
    public void String_HasExplicitByteAndWordOrder(PlcDataOrder order, ushort first, ushort second)
    {
        ushort[] data = [first, second];
        Assert.Equal("ABCD", PlcRegisterConverter.ReadString(data, order));
        ushort[] written = new ushort[2];
        PlcRegisterConverter.WriteString("ABCD", written, order);
        Assert.Equal(data, written);
    }

    [Fact]
    public void UnsupportedCharactersAndOverflow_DoNotModifyDestination()
    {
        ushort[] data = [0x1234];
        Assert.Throws<ArgumentException>(() => PlcRegisterConverter.WriteString("ABC", data));
        Assert.Throws<ArgumentException>(() => PlcRegisterConverter.WriteString("한", data));
        Assert.Equal((ushort)0x1234, data[0]);
    }

    [Fact]
    public void ReadString_RejectsNonAsciiBytes()
    {
        Assert.Throws<ArgumentException>(() => PlcRegisterConverter.ReadString(new ushort[] { 0x80FF }));
        Assert.Equal("", PlcRegisterConverter.ReadString(Array.Empty<ushort>()));
    }
}
