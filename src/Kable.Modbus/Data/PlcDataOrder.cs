namespace Kable.Modbus.Data;

/// <summary>Order relative to big-endian bytes; WordSwap reverses all words in the value.</summary>
public enum PlcDataOrder
{
    BigEndian,
    ByteSwap,
    WordSwap,
    LittleEndian
}
