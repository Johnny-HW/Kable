namespace Kable.Modbus;

using Kable.Modbus.Messages;
using Kable.Modbus.Protocol;

public sealed partial class ModbusTcpMaster
{
    public async Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken ct = default)
    {
        var request = ModbusTcpMessage.CreateReadCoils(GetNextTransactionId(), unitId ?? DefaultUnitId, startAddress, count);
        var response = await _session.RequestAsync<ModbusTcpMessage>(request, DefaultTimeout, ct).ConfigureAwait(false);
        ModbusValidation.Response(request, response);
        ValidateReadPayload(response.Payload.Span, (count + 7) / 8);
        var bits = new bool[count];
        for (int i = 0; i < count; i++)
            bits[i] = (response.Payload.Span[1 + i / 8] & (1 << (i % 8))) != 0;
        return bits;
    }

    public async Task WriteMultipleCoilsAsync(ushort startAddress, bool[] values, byte? unitId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var request = ModbusTcpMessage.CreateWriteMultipleCoils(GetNextTransactionId(), unitId ?? DefaultUnitId, startAddress, values);
        var response = await _session.RequestAsync<ModbusTcpMessage>(request, DefaultTimeout, ct).ConfigureAwait(false);
        ModbusValidation.Response(request, response);
        ModbusValidation.WriteEcho(request, response);
    }
}
