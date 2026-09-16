namespace Kable.Modbus;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IModbusMaster : IAsyncDisposable
{
    byte DefaultUnitId { get; set; }
    TimeSpan DefaultTimeout { get; set; }

    Task StartAsync(CancellationToken ct = default);
    Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken ct = default);
    Task WriteSingleRegisterAsync(ushort address, ushort value, byte? unitId = null, CancellationToken ct = default);
    Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, byte? unitId = null, CancellationToken ct = default);
}

