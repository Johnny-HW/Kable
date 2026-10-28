namespace Kable.Modbus.Data;

using System.Buffers.Binary;

/// <summary>Converts numeric register values without allocating; consumes only the required leading words.</summary>
public static partial class PlcRegisterConverter
{
    public static uint ReadUInt32(ReadOnlySpan<ushort> registers, PlcDataOrder order = PlcDataOrder.BigEndian)
    {
        Span<byte> bytes = stackalloc byte[4];
        ReadBytes(registers, bytes, order);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    public static int ReadInt32(ReadOnlySpan<ushort> registers, PlcDataOrder order = PlcDataOrder.BigEndian) =>
        unchecked((int)ReadUInt32(registers, order));

    public static float ReadSingle(ReadOnlySpan<ushort> registers, PlcDataOrder order = PlcDataOrder.BigEndian) =>
        BitConverter.Int32BitsToSingle(ReadInt32(registers, order));

    public static double ReadDouble(ReadOnlySpan<ushort> registers, PlcDataOrder order = PlcDataOrder.BigEndian)
    {
        Span<byte> bytes = stackalloc byte[8];
        ReadBytes(registers, bytes, order);
        return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(bytes));
    }

    public static void WriteUInt32(uint value, Span<ushort> destination, PlcDataOrder order = PlcDataOrder.BigEndian)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        WriteBytes(bytes, destination, order);
    }

    public static void WriteInt32(int value, Span<ushort> destination, PlcDataOrder order = PlcDataOrder.BigEndian) =>
        WriteUInt32(unchecked((uint)value), destination, order);

    public static void WriteSingle(float value, Span<ushort> destination, PlcDataOrder order = PlcDataOrder.BigEndian) =>
        WriteInt32(BitConverter.SingleToInt32Bits(value), destination, order);

    public static void WriteDouble(double value, Span<ushort> destination, PlcDataOrder order = PlcDataOrder.BigEndian)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, BitConverter.DoubleToInt64Bits(value));
        WriteBytes(bytes, destination, order);
    }

    private static void ReadBytes(ReadOnlySpan<ushort> registers, Span<byte> bytes, PlcDataOrder order)
    {
        ValidateOrder(order);
        int words = bytes.Length / 2;
        if (registers.Length < words) throw new ArgumentException("Insufficient registers.", nameof(registers));
        for (int i = 0; i < bytes.Length; i++) bytes[i] = GetByte(registers[..words], i, order);
    }

    private static void WriteBytes(ReadOnlySpan<byte> bytes, Span<ushort> destination, PlcDataOrder order)
    {
        ValidateOrder(order);
        int words = bytes.Length / 2;
        if (destination.Length < words) throw new ArgumentException("Insufficient registers.", nameof(destination));
        for (int i = 0; i < words; i++)
        {
            int logical = ReverseWords(order) ? words - 1 - i : i;
            byte high = bytes[logical * 2];
            byte low = bytes[logical * 2 + 1];
            destination[i] = SwapBytes(order) ? (ushort)((low << 8) | high) : (ushort)((high << 8) | low);
        }
    }

    private static byte GetByte(ReadOnlySpan<ushort> registers, int logicalByte, PlcDataOrder order)
    {
        int word = logicalByte / 2;
        if (ReverseWords(order)) word = registers.Length - 1 - word;
        bool high = logicalByte % 2 == 0;
        if (SwapBytes(order)) high = !high;
        return high ? (byte)(registers[word] >> 8) : (byte)registers[word];
    }

    private static bool ReverseWords(PlcDataOrder order) => order is PlcDataOrder.WordSwap or PlcDataOrder.LittleEndian;
    private static bool SwapBytes(PlcDataOrder order) => order is PlcDataOrder.ByteSwap or PlcDataOrder.LittleEndian;
    private static void ValidateOrder(PlcDataOrder order)
    {
        if (order < PlcDataOrder.BigEndian || order > PlcDataOrder.LittleEndian)
            throw new ArgumentOutOfRangeException(nameof(order));
    }
}
