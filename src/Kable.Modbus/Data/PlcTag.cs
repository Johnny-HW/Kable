namespace Kable.Modbus.Data;

public enum PlcValueKind { UInt16, Int16, UInt32, Int32, Single, Double, Ascii }
public enum PlcRegisterArea { Holding, Input }

public sealed record PlcTag(string Name, ushort Address, PlcValueKind Kind,
    PlcDataOrder Order = PlcDataOrder.BigEndian, double Scale = 1, double Offset = 0,
    string Unit = "", int StringRegisters = 0, PlcRegisterArea Area = PlcRegisterArea.Holding)
{
    internal ushort GetCount()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (!double.IsFinite(Scale) || Scale == 0 || !double.IsFinite(Offset)) throw new ArgumentOutOfRangeException(nameof(Scale));
        if (!Enum.IsDefined(Order) || !Enum.IsDefined(Area)) throw new ArgumentOutOfRangeException(nameof(Order));
        int count = Kind switch
        {
            PlcValueKind.UInt16 or PlcValueKind.Int16 => 1,
            PlcValueKind.UInt32 or PlcValueKind.Int32 or PlcValueKind.Single => 2,
            PlcValueKind.Double => 4,
            PlcValueKind.Ascii => StringRegisters,
            _ => throw new ArgumentOutOfRangeException(nameof(Kind))
        };
        Protocol.ModbusValidation.Range(Address, count, 125);
        if (Kind == PlcValueKind.Ascii && (Scale != 1 || Offset != 0)) throw new ArgumentException("ASCII tags cannot be scaled.");
        return (ushort)count;
    }
}

public sealed record PlcTagValue(PlcTag Tag, object RawValue, object Value);
