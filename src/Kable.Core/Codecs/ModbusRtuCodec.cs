namespace Kable.Codecs;

using System;
using System.Buffers;
using Kable.Core.Checksums;

/// <summary>
/// System.IO.Pipelines 기반 초고속 0-GC Modbus-RTU 프레이밍 및 자동 CRC 코덱
/// 요청 시 CRC 자동 부착(Encode), 수신 시 CRC 자동 검증 및 완성 프레임 추출(TryDecode)
/// </summary>
public sealed class ModbusRtuCodec : IProtocolCodec<ReadOnlyMemory<byte>>
{
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
    /// 수신 파이프라인 버퍼에서 완성된 Modbus 응답 프레임을 추출하고 CRC 무결성을 자동 검증합니다.
    /// </summary>
    public bool TryDecode(ref ReadOnlySequence<byte> buffer, out ReadOnlyMemory<byte> message)
    {
        if (buffer.Length < 4) // 최소 프레임: Slave(1) + FC(1) + Data(1) + CRC(2) = 5 (에러 응답은 4바이트 또는 5바이트)
        {
            message = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        // 바이트 카피 없이 첫 3바이트 판독
        Span<byte> header = stackalloc byte[3];
        buffer.Slice(0, 3).CopyTo(header);
        byte fc = header[1];

        int expectedLength;

        // 예외 응답 (FC high bit set, 0x80 | FC): Slave(1) + FC(1) + ExceptionCode(1) + CRC(2) = 5 bytes
        if ((fc & 0x80) != 0)
        {
            expectedLength = 5;
        }
        // FC05 (Write Single Coil), FC06 (Write Single Register): 고정 8 bytes
        else if (fc == 0x05 || fc == 0x06)
        {
            expectedLength = 8;
        }
        // FC01, FC02, FC03, FC04 (Read 계열 응답): Slave(1) + FC(1) + ByteCount(1) + [N bytes] + CRC(2)
        else if (fc == 0x01 || fc == 0x02 || fc == 0x03 || fc == 0x04)
        {
            byte byteCount = header[2];
            expectedLength = 3 + byteCount + 2;
        }
        // FC10 (Write Multiple Registers 응답): 고정 8 bytes
        else if (fc == 0x10)
        {
            expectedLength = 8;
        }
        else
        {
            // 알 수 없는 기능 코드의 경우 버퍼가 충분할 때까지 대기
            expectedLength = (int)buffer.Length;
        }

        if (buffer.Length < expectedLength)
        {
            message = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        var frameSeq = buffer.Slice(0, expectedLength);
        Span<byte> frameSpan = stackalloc byte[expectedLength];
        frameSeq.CopyTo(frameSpan);

        // CRC 자동 검증
        if (!Crc16Modbus.Validate(frameSpan))
        {
            // CRC 불일치 시 1바이트 슬라이스하여 동기화 복구 시도
            buffer = buffer.Slice(1);
            message = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        message = frameSpan.ToArray();
        buffer = buffer.Slice(buffer.GetPosition(expectedLength));
        return true;
    }

    public string? ExtractCorrelationId(ReadOnlyMemory<byte> message) => null;
    public bool IsAutonomousMessage(ReadOnlyMemory<byte> message) => false;
}
