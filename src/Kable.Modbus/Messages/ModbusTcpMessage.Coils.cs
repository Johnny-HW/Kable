namespace Kable.Modbus.Messages;

using System.Buffers.Binary;
using Kable.Modbus.Protocol;

public sealed partial class ModbusTcpMessage
{
    public static ModbusTcpMessage CreateReadCoils(ushort transactionId, byte unitId, ushort startAddress, ushort quantity)
    {
        ModbusValidation.Range(startAddress, quantity, 2000);
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(payload, startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2), quantity);
        return new(transactionId, unitId, 1, payload);
    }

    public static ModbusTcpMessage CreateWriteMultipleCoils(ushort transactionId, byte unitId,
        ushort startAddress, ReadOnlySpan<bool> values)
    {
        ModbusValidation.Range(startAddress, values.Length, 1968);
        int byteCount = (values.Length + 7) / 8;
        byte[] payload = new byte[5 + byteCount];
        BinaryPrimitives.WriteUInt16BigEndian(payload, startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2), (ushort)values.Length);
        payload[4] = (byte)byteCount;
        for (int i = 0; i < values.Length; i++)
            if (values[i]) payload[5 + i / 8] |= (byte)(1 << (i % 8));
        return new(transactionId, unitId, 15, payload);
    }
}
