namespace Kable.Modbus.Messages;

using System;
using System.Buffers.Binary;

using Kable.Observability;

/// <summary>
/// Modbus-TCP MBAP 헤더 및 PDU를 캡슐화하는 불변 메시지 레코드.
/// </summary>
public sealed partial class ModbusTcpMessage : IRawByteProvider
{
    public ushort TransactionId { get; }
    public ushort ProtocolId { get; }
    public byte UnitId { get; }
    public byte FunctionCode { get; }
    public ReadOnlyMemory<byte> Payload { get; }

    public bool IsException => (FunctionCode & 0x80) != 0;
    public byte ExceptionCode => IsException && Payload.Length > 0 ? Payload.Span[0] : (byte)0;

    public ReadOnlyMemory<byte> ToRawBytes()
    {
        int pduLength = 1 + Payload.Length;
        int mbapLength = 1 + pduLength;
        int totalLength = 6 + mbapLength;
        byte[] bytes = new byte[totalLength];
        var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(0, 2), TransactionId);
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(2, 2), ProtocolId);
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(4, 2), (ushort)mbapLength);
        span[6] = UnitId;
        span[7] = FunctionCode;
        if (!Payload.IsEmpty)
        {
            Payload.Span.CopyTo(span.Slice(8));
        }
        return bytes;
    }

    public override string ToString()
    {
        if (IsException)
        {
            return $"FC{FunctionCode & 0x7F:D2} Exception (0x{ExceptionCode:X2})";
        }

        switch (FunctionCode)
        {
            case 0x02:
                if (Payload.Length == 4)
                {
                    ushort addr = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(0, 2));
                    ushort qty = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(2, 2));
                    return $"FC02 ReadDiscreteInputs Addr={addr} Qty={qty}";
                }
                if (Payload.Length >= 1)
                {
                    byte count = Payload.Span[0];
                    return $"FC02 ReadDiscreteInputs Response [{count} bytes]";
                }
                return "FC02 ReadDiscreteInputs";

            case 0x03:
                if (Payload.Length == 4)
                {
                    ushort addr = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(0, 2));
                    ushort qty = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(2, 2));
                    return $"FC03 ReadHoldingRegs Addr={addr} Qty={qty}";
                }
                if (Payload.Length >= 1)
                {
                    byte count = Payload.Span[0];
                    return $"FC03 ReadHoldingRegs Response [{count / 2} regs]";
                }
                return "FC03 ReadHoldingRegs";

            case 0x04:
                if (Payload.Length == 4)
                {
                    ushort addr = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(0, 2));
                    ushort qty = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(2, 2));
                    return $"FC04 ReadInputRegs Addr={addr} Qty={qty}";
                }
                if (Payload.Length >= 1)
                {
                    byte count = Payload.Span[0];
                    return $"FC04 ReadInputRegs Response [{count / 2} regs]";
                }
                return "FC04 ReadInputRegs";

            case 0x05:
                if (Payload.Length >= 4)
                {
                    ushort addr = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(0, 2));
                    ushort val = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(2, 2));
                    return $"FC05 WriteSingleCoil Coil={addr} Val={(val == 0xFF00 ? "ON" : "OFF")}";
                }
                return "FC05 WriteSingleCoil";

            case 0x06:
                if (Payload.Length >= 4)
                {
                    ushort addr = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(0, 2));
                    ushort val = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(2, 2));
                    return $"FC06 WriteSingleRegister Addr={addr} Val=0x{val:X4}";
                }
                return "FC06 WriteSingleRegister";

            case 0x10:
                if (Payload.Length >= 4)
                {
                    ushort addr = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(0, 2));
                    ushort qty = BinaryPrimitives.ReadUInt16BigEndian(Payload.Span.Slice(2, 2));
                    return $"FC16 WriteMultipleRegisters Addr={addr} Qty={qty}";
                }
                return "FC16 WriteMultipleRegisters";

            default:
                return $"FC{FunctionCode:D2} TransId={TransactionId} Unit={UnitId} Len={Payload.Length}";
        }
    }

    public ModbusTcpMessage(ushort transactionId, byte unitId, byte functionCode, ReadOnlyMemory<byte> payload, ushort protocolId = 0)
    {
        TransactionId = transactionId;
        UnitId = unitId;
        FunctionCode = functionCode;
        Payload = payload;
        ProtocolId = protocolId;
    }

    /// <summary>
    /// FC02 Read Discrete Inputs 요청 생성 헬퍼
    /// </summary>
    public static ModbusTcpMessage CreateReadDiscreteInputs(ushort transactionId, byte unitId, ushort startAddress, ushort quantity)
    {
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), quantity);
        return new ModbusTcpMessage(transactionId, unitId, 0x02, payload);
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
    /// FC05 Write Single Coil 요청 생성 헬퍼
    /// </summary>
    public static ModbusTcpMessage CreateWriteSingleCoil(ushort transactionId, byte unitId, ushort address, bool state)
    {
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), state ? (ushort)0xFF00 : (ushort)0x0000);
        return new ModbusTcpMessage(transactionId, unitId, 0x05, payload);
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
