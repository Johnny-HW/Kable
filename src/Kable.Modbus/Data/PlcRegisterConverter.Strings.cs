namespace Kable.Modbus.Data;

using System.Buffers;

public static partial class PlcRegisterConverter
{
    /// <summary>Reads strict ASCII across the entire supplied field; trims trailing NUL padding only.</summary>
    public static string ReadString(ReadOnlySpan<ushort> registers, PlcDataOrder order = PlcDataOrder.BigEndian,
        bool trimNullPadding = true)
    {
        ValidateOrder(order);
        int length = checked(registers.Length * 2);
        char[]? rented = null;
        Span<char> chars = length <= 256 ? stackalloc char[length] : (rented = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            for (int i = 0; i < length; i++)
            {
                byte value = GetByte(registers, i, order);
                if (value > 127) throw new ArgumentException("Register field contains non-ASCII bytes.", nameof(registers));
                chars[i] = (char)value;
            }
            if (trimNullPadding)
                while (length > 0 && chars[length - 1] == '\0') length--;
            return new string(chars[..length]);
        }
        finally
        {
            if (rented != null) ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>Writes strict ASCII and zero-pads the entire destination. Word order covers the entire field.</summary>
    public static void WriteString(ReadOnlySpan<char> value, Span<ushort> destination, PlcDataOrder order = PlcDataOrder.BigEndian)
    {
        ValidateOrder(order);
        if (value.Length > (long)destination.Length * 2) throw new ArgumentException("String exceeds field capacity.", nameof(value));
        foreach (char character in value)
            if (character > 127) throw new ArgumentException("Only ASCII characters are supported.", nameof(value));

        for (int i = 0; i < destination.Length; i++)
        {
            int logical = ReverseWords(order) ? destination.Length - 1 - i : i;
            long index = (long)logical * 2;
            byte high = index < value.Length ? (byte)value[(int)index] : (byte)0;
            byte low = index + 1 < value.Length ? (byte)value[(int)index + 1] : (byte)0;
            destination[i] = SwapBytes(order) ? (ushort)((low << 8) | high) : (ushort)((high << 8) | low);
        }
    }
}
