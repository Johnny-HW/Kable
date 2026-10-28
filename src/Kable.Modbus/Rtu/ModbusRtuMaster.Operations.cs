namespace Kable.Modbus.Rtu;

public sealed partial class ModbusRtuMaster
{
    public Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken ct = default) =>
        _operations.ReadCoilsAsync(startAddress, count, unitId, ct);
    public Task<bool[]> ReadDiscreteInputsAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken ct = default) =>
        _operations.ReadDiscreteInputsAsync(startAddress, count, unitId, ct);
    public Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken ct = default) =>
        _operations.ReadHoldingRegistersAsync(startAddress, count, unitId, ct);
    public Task<ushort[]> ReadInputRegistersAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken ct = default) =>
        _operations.ReadInputRegistersAsync(startAddress, count, unitId, ct);
    public Task WriteSingleCoilAsync(ushort address, bool value, byte? unitId = null, CancellationToken ct = default) =>
        _operations.WriteSingleCoilAsync(address, value, unitId, ct);
    public Task WriteSingleRegisterAsync(ushort address, ushort value, byte? unitId = null, CancellationToken ct = default) =>
        _operations.WriteSingleRegisterAsync(address, value, unitId, ct);
    public Task WriteMultipleCoilsAsync(ushort startAddress, bool[] values, byte? unitId = null, CancellationToken ct = default) =>
        _operations.WriteMultipleCoilsAsync(startAddress, values, unitId, ct);
    public Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, byte? unitId = null, CancellationToken ct = default) =>
        _operations.WriteMultipleRegistersAsync(startAddress, values, unitId, ct);
}
