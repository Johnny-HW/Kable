namespace Kable.Modbus.Tests.Data;

using Kable.Modbus.Data;
using Xunit;

public sealed class PlcRegisterConverterTests
{
    [Theory]
    [InlineData(PlcDataOrder.BigEndian, 0x1234, 0x5678)]
    [InlineData(PlcDataOrder.ByteSwap, 0x3412, 0x7856)]
    [InlineData(PlcDataOrder.WordSwap, 0x5678, 0x1234)]
    [InlineData(PlcDataOrder.LittleEndian, 0x7856, 0x3412)]
    public void UInt32_UsesKnownLayout(PlcDataOrder order, ushort first, ushort second)
    {
        ushort[] registers = [first, second];
        Assert.Equal(0x12345678u, PlcRegisterConverter.ReadUInt32(registers, order));
        ushort[] written = new ushort[2];
        PlcRegisterConverter.WriteUInt32(0x12345678, written, order);
        Assert.Equal(registers, written);
    }

    [Theory]
    [InlineData(PlcDataOrder.BigEndian)]
    [InlineData(PlcDataOrder.ByteSwap)]
    [InlineData(PlcDataOrder.WordSwap)]
    [InlineData(PlcDataOrder.LittleEndian)]
    public void NumericValues_RoundTrip(PlcDataOrder order)
    {
        ushort[] registers = new ushort[4];
        PlcRegisterConverter.WriteInt32(-1234567, registers, order);
        Assert.Equal(-1234567, PlcRegisterConverter.ReadInt32(registers, order));
        PlcRegisterConverter.WriteSingle(-12.5f, registers, order);
        Assert.Equal(-12.5f, PlcRegisterConverter.ReadSingle(registers, order));
        PlcRegisterConverter.WriteDouble(-12345.125, registers, order);
        Assert.Equal(-12345.125, PlcRegisterConverter.ReadDouble(registers, order));
    }

    [Fact]
    public void FloatingPoint_UsesIeee754AndPreservesNaNBits()
    {
        Assert.Equal(1.0f, PlcRegisterConverter.ReadSingle(new ushort[] { 0x3F80, 0 }));
        Assert.Equal(1.0, PlcRegisterConverter.ReadDouble(new ushort[] { 0x3FF0, 0, 0, 0 }));
        ushort[] data = [0x7FC0, 0x1234];
        var value = PlcRegisterConverter.ReadSingle(data);
        Assert.True(float.IsNaN(value));
        ushort[] written = new ushort[2];
        PlcRegisterConverter.WriteSingle(value, written);
        Assert.Equal(data, written);
    }

    [Fact]
    public void Double_WordSwapReversesAllFourWords()
    {
        ushort[] normal = [0x4009, 0x21FB, 0x5444, 0x2D18];
        ushort[] reversed = [0x2D18, 0x5444, 0x21FB, 0x4009];
        Assert.Equal(Math.PI, PlcRegisterConverter.ReadDouble(normal));
        Assert.Equal(Math.PI, PlcRegisterConverter.ReadDouble(reversed, PlcDataOrder.WordSwap));
    }

    [Fact]
    public void InvalidBuffersAndOrder_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => PlcRegisterConverter.ReadUInt32(new ushort[1]));
        Assert.Throws<ArgumentException>(() => PlcRegisterConverter.WriteDouble(1, new ushort[3]));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlcRegisterConverter.ReadUInt32(new ushort[2], (PlcDataOrder)99));
    }

    [Fact]
    public void NumericConversion_DoesNotAllocateAfterWarmup()
    {
        ushort[] registers = new ushort[4];
        for (int i = 0; i < 100; i++) PlcRegisterConverter.WriteDouble(i, registers);
        long before = GC.GetAllocatedBytesForCurrentThread();
        double sum = 0;
        for (int i = 0; i < 1000; i++)
        {
            PlcRegisterConverter.WriteDouble(i, registers, PlcDataOrder.LittleEndian);
            sum += PlcRegisterConverter.ReadDouble(registers, PlcDataOrder.LittleEndian);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(499500, sum);
        Assert.Equal(0, allocated);
    }
}
