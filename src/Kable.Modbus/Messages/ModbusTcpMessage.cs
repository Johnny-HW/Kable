namespace Kable.Modbus.Messages;

using System;
using System.Buffers.Binary;

/// <summary>
/// Modbus-TCP MBAP 헤더 및 PDU를 캡슐화하는 불변 메시지 레코드.
/// </summary>
public sealed class ModbusTcpMessage
{
    public ushort TransactionId { get; }
    public ushort ProtocolId { get; }
    public byte UnitId { get; }
    public byte FunctionCode { get; }
    public ReadOnlyMemory<byte> Payload { get; }

    public bool IsException => (FunctionCode & 0x80) != 0;
    public byte ExceptionCode => IsException && Payload.Length > 0 ? Payload.Span[0] : (byte)0;

    public ModbusTcpMessage(ushort transactionId, byte unitId, byte functionCode, ReadOnlyMemory<byte> payload, ushort protocolId = 0)
    {
        TransactionId = transactionId;
        UnitId = unitId;
        FunctionCode = functionCode;
        Payload = payload;
        ProtocolId = protocolId;
    }

    /// <summary>
    /// FC03 / FC04 Read Holding/Input Registers 요청 생성 헬퍼
    /// </summary>
    public static ModbusTcpMessage CreateReadRegisters(ushort transactionId, byte unitId, byte functionCode, ushort startAddress, ushort quantity)
    {
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), quantity);
        return new ModbusTcpMessage(transactionId, unitId, functionCode, payload);
    }

    /// <summary>
    /// FC06 Write Single Register 요청 생성 헬퍼
    /// </summary>
    public static ModbusTcpMessage CreateWriteSingleRegister(ushort transactionId, byte unitId, ushort address, ushort value)
    {
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), value);
        return new ModbusTcpMessage(transactionId, unitId, 0x06, payload);
    }

    /// <summary>
    /// FC16 Write Multiple Registers 요청 생성 헬퍼
    /// </summary>
    public static ModbusTcpMessage CreateWriteMultipleRegisters(ushort transactionId, byte unitId, ushort startAddress, ReadOnlySpan<ushort> values)
    {
        int byteCount = values.Length * 2;
        byte[] payload = new byte[5 + byteCount];

        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), (ushort)values.Length);
        payload[4] = (byte)byteCount;

        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(5 + (i * 2), 2), values[i]);
        }

        return new ModbusTcpMessage(transactionId, unitId, 0x10, payload);
    }
}
