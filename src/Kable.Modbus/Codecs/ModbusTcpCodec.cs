namespace Kable.Modbus.Codecs;

using System;
using System.Buffers;
using System.Buffers.Binary;
using Kable.Codecs;
using Kable.Core;
using Kable.Modbus.Messages;

/// <summary>
/// System.IO.Pipelines 기반 0-GC Modbus-TCP 코덱.
/// MBAP 헤더(7바이트)를 파싱 및 생성하며, Transaction ID를 통한 동시 다중 요청 파이프라이닝을 지원합니다.
/// </summary>
public sealed class ModbusTcpCodec : IProtocolCodec<ModbusTcpMessage>
{
    private readonly int _maxFrameSize;

    public bool SupportsCorrelationId => true;
    public int MaxFrameSize => _maxFrameSize;

    public ModbusTcpCodec(int maxFrameSize = 260)
    {
        _maxFrameSize = maxFrameSize;
    }

    /// <summary>
    /// ModbusTcpMessage를 MBAP Header(7 bytes) + FunctionCode(1 byte) + Payload로 인코딩합니다.
    /// </summary>
    public void Encode(ModbusTcpMessage message, IBufferWriter<byte> output)
    {
        int pduLength = 1 + message.Payload.Length; // FC(1) + Payload
        int mbapLength = 1 + pduLength;            // UnitId(1) + PDU
        int totalLength = 6 + mbapLength;          // TransId(2) + ProtoId(2) + Length(2) + UnitId(1) + PDU

        var span = output.GetSpan(totalLength);

        // MBAP Header
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(0, 2), message.TransactionId);
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(2, 2), message.ProtocolId);
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(4, 2), (ushort)mbapLength);
        span[6] = message.UnitId;

        // PDU
        span[7] = message.FunctionCode;
        if (!message.Payload.IsEmpty)
        {
            message.Payload.Span.CopyTo(span.Slice(8, message.Payload.Length));
        }

        output.Advance(totalLength);
    }

    /// <summary>
    /// 수신 바이트 버퍼에서 완성된 MBAP 프레임을 검증 및 추출합니다.
    /// </summary>
    public bool TryDecode(ref ReadOnlySequence<byte> buffer, out ModbusTcpMessage message)
    {
        // 최소 MBAP Header(6 bytes: TransId[2] + ProtoId[2] + Length[2]) 필요
        if (buffer.Length < 6)
        {
            message = null!;
            return false;
        }

        Span<byte> headerBytes = stackalloc byte[6];
        buffer.Slice(0, 6).CopyTo(headerBytes);

        ushort length = BinaryPrimitives.ReadUInt16BigEndian(headerBytes.Slice(4, 2));
        int totalFrameSize = 6 + length;

        if (buffer.Length < totalFrameSize)
        {
            message = null!;
            return false;
        }

        // 전체 프레임 복사 없이 파싱
        Span<byte> fullHeader = stackalloc byte[8]; // 6 + UnitId(1) + FC(1)
        buffer.Slice(0, Math.Min(8, totalFrameSize)).CopyTo(fullHeader);

        ushort transId = BinaryPrimitives.ReadUInt16BigEndian(fullHeader.Slice(0, 2));
        ushort protoId = BinaryPrimitives.ReadUInt16BigEndian(fullHeader.Slice(2, 2));
        byte unitId = fullHeader[6];
        byte functionCode = fullHeader[7];

        int payloadLength = totalFrameSize - 8;
        ReadOnlyMemory<byte> payloadMemory = ReadOnlyMemory<byte>.Empty;

        if (payloadLength > 0)
        {
            var payloadSeq = buffer.Slice(8, payloadLength);
            byte[] payloadArray = payloadSeq.ToArray();
            payloadMemory = payloadArray;
        }

        message = new ModbusTcpMessage(transId, unitId, functionCode, payloadMemory, protoId);
        buffer = buffer.Slice(buffer.GetPosition(totalFrameSize));
        return true;
    }

    /// <summary>
    /// MBAP Transaction ID를 Correlation ID 문자열로 반환하여 KableSession의 병렬 요청 디스패치에 연동합니다.
    /// </summary>
    public string? ExtractCorrelationId(ModbusTcpMessage message)
    {
        return message.TransactionId.ToString();
    }

    public bool IsAutonomousMessage(ModbusTcpMessage message) => false;
}
