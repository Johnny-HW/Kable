namespace Kable.Codecs;

using System;
using System.Buffers;
using Kable.Core.Checksums;

/// <summary>
/// System.IO.Pipelines 기반 초고속 0-GC Modbus-RTU 프레이밍 및 자동 CRC 코덱.
/// Encode는 PDU에 CRC-16을 부착한다. TryDecode는 마스터 응답과 슬레이브 요청을
/// 후보 길이 + CRC로 구분한다 (FC01-04 8바이트 요청 vs ByteCount 응답, FC15/16 가변 요청 vs 8바이트 ACK).
/// </summary>
public sealed partial class ModbusRtuCodec : IProtocolCodec<ReadOnlyMemory<byte>>
{
    private const int StackAllocThreshold = 256;
    private const int ReadRequestLength = 8;
    private const int WriteSingleLength = 8;
    private const int ExceptionLength = 5;
    private const int WriteMultipleAckLength = 8;
    private const int WriteMultipleRequestPreamble = 7;

    private readonly int _maxFrameSize;

    public bool SupportsCorrelationId => false;
    public int MaxFrameSize => _maxFrameSize;

    public ModbusRtuCodec(int maxFrameSize = 256)
    {
        _maxFrameSize = maxFrameSize;
    }

    /// <summary>
    /// 순수 Modbus PDU 페이로드를 인코딩하면서 끝에 2바이트 CRC-16을 자동으로 부착합니다.
    /// </summary>
    public void Encode(ReadOnlyMemory<byte> message, IBufferWriter<byte> output)
    {
        var span = output.GetSpan(message.Length + 2);
        Crc16Modbus.Append(message.Span, span);
        output.Advance(message.Length + 2);
    }

    /// <summary>
    /// 수신 파이프라인 버퍼에서 완성된 Modbus 프레임을 추출하고 CRC 무결성을 자동 검증합니다.
    /// </summary>
    public bool TryDecode(ref ReadOnlySequence<byte> buffer, out ReadOnlyMemory<byte> message)
    {
        if (buffer.Length < 4)
        {
            message = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        Span<byte> header = stackalloc byte[3];
        buffer.Slice(0, 3).CopyTo(header);
        byte fc = header[1];

        if (fc >= 0x01 && fc <= 0x04 && buffer.Length < 8)
        {
            return TryDecodeShortRead(ref buffer, header[2], out message);
        }

        if ((fc & 0x80) != 0)
        {
            return TryFinishFixed(ref buffer, ExceptionLength, out message);
        }

        if (fc == 0x05 || fc == 0x06)
        {
            return TryFinishFixed(ref buffer, WriteSingleLength, out message);
        }

        if (fc == 0x01 || fc == 0x02 || fc == 0x03 || fc == 0x04)
        {
            return TryDecodeReadFunction(ref buffer, header[2], out message);
        }

        if (fc == 0x0F || fc == 0x10)
        {
            return TryDecodeWriteMultiple(ref buffer, out message);
        }

        return TryFinishFixed(ref buffer, (int)buffer.Length, out message);
    }

    public string? ExtractCorrelationId(ReadOnlyMemory<byte> message) => null;
    public bool IsAutonomousMessage(ReadOnlyMemory<byte> message) => false;

    private bool TryDecodeReadFunction(ref ReadOnlySequence<byte> buffer, byte byteCount, out ReadOnlyMemory<byte> message)
    {
        int responseLength = 3 + byteCount + 2;
        bool responseInRange = responseLength >= 5 && responseLength <= _maxFrameSize;

        if (responseInRange && buffer.Length >= responseLength)
        {
            if (TryConsumeValidFrame(ref buffer, responseLength, out message))
            {
                return true;
            }
        }

        if (buffer.Length >= ReadRequestLength && ReadRequestLength <= _maxFrameSize)
        {
            if (TryConsumeValidFrame(ref buffer, ReadRequestLength, out message))
            {
                return true;
            }
        }

        if (!responseInRange)
        {
            return DropByte(ref buffer, out message);
        }

        int needed = Math.Max(ReadRequestLength, responseLength);
        if (buffer.Length < needed)
        {
            message = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        return DropByte(ref buffer, out message);
    }

    private bool TryDecodeWriteMultiple(ref ReadOnlySequence<byte> buffer, out ReadOnlyMemory<byte> message)
    {
        if (buffer.Length >= WriteMultipleAckLength)
        {
            if (TryConsumeValidFrame(ref buffer, WriteMultipleAckLength, out message))
            {
                return true;
            }
        }

        if (buffer.Length < WriteMultipleRequestPreamble)
        {
            message = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        Span<byte> preamble = stackalloc byte[WriteMultipleRequestPreamble];
        buffer.Slice(0, WriteMultipleRequestPreamble).CopyTo(preamble);
        int requestLength = 9 + preamble[6];

        if (requestLength > _maxFrameSize)
        {
            return DropByte(ref buffer, out message);
        }

        if (buffer.Length < requestLength)
        {
            message = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        if (TryConsumeValidFrame(ref buffer, requestLength, out message))
        {
            return true;
        }

        return DropByte(ref buffer, out message);
    }

    private bool TryFinishFixed(ref ReadOnlySequence<byte> buffer, int expectedLength, out ReadOnlyMemory<byte> message)
    {
        if (expectedLength > _maxFrameSize)
        {
            return DropByte(ref buffer, out message);
        }

        if (buffer.Length < expectedLength)
        {
            message = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        if (TryConsumeValidFrame(ref buffer, expectedLength, out message))
        {
            return true;
        }

        return DropByte(ref buffer, out message);
    }

    private bool TryConsumeValidFrame(ref ReadOnlySequence<byte> buffer, int length, out ReadOnlyMemory<byte> message)
    {
        message = ReadOnlyMemory<byte>.Empty;
        if (length < 4 || length > _maxFrameSize || buffer.Length < length)
        {
            return false;
        }

        var frameSeq = buffer.Slice(0, length);
        byte[]? rented = null;
        Span<byte> frameSpan = length <= StackAllocThreshold
            ? stackalloc byte[length]
            : (rented = ArrayPool<byte>.Shared.Rent(length)).AsSpan(0, length);

        try
        {
            frameSeq.CopyTo(frameSpan);

            if (!Crc16Modbus.Validate(frameSpan))
            {
                return false;
            }

            message = frameSpan.ToArray();
            buffer = buffer.Slice(buffer.GetPosition(length));
            return true;
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private static bool DropByte(ref ReadOnlySequence<byte> buffer, out ReadOnlyMemory<byte> message)
    {
        buffer = buffer.Slice(1);
        message = ReadOnlyMemory<byte>.Empty;
        return false;
    }
}
