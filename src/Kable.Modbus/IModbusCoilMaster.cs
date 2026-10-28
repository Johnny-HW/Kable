namespace Kable.Modbus;

/// <summary>Optional Coil capabilities without changing existing IModbusMaster implementations.</summary>
public interface IModbusCoilMaster : IModbusMaster
{
    Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken ct = default);
    Task WriteMultipleCoilsAsync(ushort startAddress, bool[] values, byte? unitId = null, CancellationToken ct = default);
}
