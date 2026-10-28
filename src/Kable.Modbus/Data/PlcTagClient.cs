namespace Kable.Modbus.Data;

using System.Globalization;

public sealed class PlcTagClient(IModbusMaster master)
{
    public async Task<PlcTagValue> ReadAsync(PlcTag tag, byte? unitId = null, CancellationToken ct = default)
    {
        ushort count = tag.GetCount();
        var words = tag.Area == PlcRegisterArea.Holding
            ? await master.ReadHoldingRegistersAsync(tag.Address, count, unitId, ct).ConfigureAwait(false)
            : await master.ReadInputRegistersAsync(tag.Address, count, unitId, ct).ConfigureAwait(false);
        object raw = tag.Kind switch
        {
            PlcValueKind.UInt16 => Word(words[0], tag.Order),
            PlcValueKind.Int16 => unchecked((short)Word(words[0], tag.Order)),
            PlcValueKind.UInt32 => PlcRegisterConverter.ReadUInt32(words, tag.Order),
            PlcValueKind.Int32 => PlcRegisterConverter.ReadInt32(words, tag.Order),
            PlcValueKind.Single => PlcRegisterConverter.ReadSingle(words, tag.Order),
            PlcValueKind.Double => PlcRegisterConverter.ReadDouble(words, tag.Order),
            PlcValueKind.Ascii => PlcRegisterConverter.ReadString(words, tag.Order),
            _ => throw new ArgumentOutOfRangeException(nameof(tag))
        };
        object value = raw is string ? raw : Convert.ToDouble(raw, CultureInfo.InvariantCulture) * tag.Scale + tag.Offset;
        return new(tag, raw, value);
    }

    public async Task WriteAsync(PlcTag tag, object value, byte? unitId = null, CancellationToken ct = default)
    {
        ushort count = tag.GetCount();
        if (tag.Area != PlcRegisterArea.Holding) throw new InvalidOperationException("Input registers are read-only.");
        var words = new ushort[count];
        if (tag.Kind == PlcValueKind.Ascii)
        {
            if (value is not string text) throw new ArgumentException("ASCII tag requires a string.", nameof(value));
            PlcRegisterConverter.WriteString(text, words, tag.Order);
        }
        else
        {
            double raw = (Convert.ToDouble(value, CultureInfo.InvariantCulture) - tag.Offset) / tag.Scale;
            if (!double.IsFinite(raw)) throw new ArgumentOutOfRangeException(nameof(value));
            if (tag.Kind is PlcValueKind.UInt16 or PlcValueKind.Int16 or PlcValueKind.UInt32 or PlcValueKind.Int32)
            {
                double rounded = Math.Round(raw);
                if (Math.Abs(raw - rounded) > Math.Max(1, Math.Abs(raw)) * 1e-12)
                    throw new ArgumentException("Scaled value is not an integral register value.", nameof(value));
                raw = rounded;
            }
            switch (tag.Kind)
            {
                case PlcValueKind.UInt16: words[0] = Word(checked((ushort)raw), tag.Order); break;
                case PlcValueKind.Int16: words[0] = Word(unchecked((ushort)checked((short)raw)), tag.Order); break;
                case PlcValueKind.UInt32: PlcRegisterConverter.WriteUInt32(checked((uint)raw), words, tag.Order); break;
                case PlcValueKind.Int32: PlcRegisterConverter.WriteInt32(checked((int)raw), words, tag.Order); break;
                case PlcValueKind.Single:
                    if (Math.Abs(raw) > float.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
                    PlcRegisterConverter.WriteSingle((float)raw, words, tag.Order); break;
                case PlcValueKind.Double: PlcRegisterConverter.WriteDouble(raw, words, tag.Order); break;
            }
        }
        await master.WriteMultipleRegistersAsync(tag.Address, words, unitId, ct).ConfigureAwait(false);
    }

    private static ushort Word(ushort word, PlcDataOrder order) =>
        order is PlcDataOrder.ByteSwap or PlcDataOrder.LittleEndian ? (ushort)((word << 8) | (word >> 8)) : word;
}
