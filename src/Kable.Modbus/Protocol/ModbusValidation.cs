namespace Kable.Modbus.Protocol;

using Kable.Modbus.Messages;

internal static class ModbusValidation
{
    internal static void Range(ushort address, int count, int maximum)
    {
        if (count < 1 || count > maximum || (int)address + count > 65536)
            throw new ArgumentOutOfRangeException(nameof(count), "Invalid Modbus quantity or address range.");
    }

    internal static void Response(ModbusTcpMessage request, ModbusTcpMessage response)
    {
        if (response.ProtocolId != 0 || response.UnitId != request.UnitId ||
            (response.FunctionCode != request.FunctionCode && response.FunctionCode != (request.FunctionCode | 0x80)))
            throw new InvalidOperationException("Modbus response protocol, unit or function does not match the request.");

        if (response.IsException)
        {
            if (response.Payload.Length != 1)
                throw new InvalidOperationException("Malformed Modbus exception response.");
            throw new InvalidOperationException($"Modbus Exception Response: Code 0x{response.ExceptionCode:X2}");
        }
    }

    internal static void WriteEcho(ModbusTcpMessage request, ModbusTcpMessage response)
    {
        if (response.Payload.Length != 4 || !response.Payload.Span.SequenceEqual(request.Payload.Span[..4]))
            throw new InvalidOperationException("Modbus write acknowledgement does not match the request.");
    }
}
