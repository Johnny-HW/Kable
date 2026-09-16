namespace Kable.Melsec.Codecs;

using System;
using System.Buffers;
using System.Buffers.Binary;
using Kable.Codecs;
using Kable.Melsec.Protocol;

/// <summary>
/// 미쓰비시 SLMP / MC Protocol 3E 바이너리 프레임 코덱.
/// System.IO.Pipelines의 0-GC 스트림 위에서 요청/응답 패킷을 파싱하고 생성합니다.
/// </summary>
public sealed class MelsecSlmpCodec : IProtocolCodec<Slmp3EFrame>
{
    private readonly int _maxFrameSize;

    // MC Protocol 3E TCP 규격은 요청 헤더에 트랜잭션 ID가 기본 내장되지 않고 FIFO 1:1 트랜잭션으로 동작
    public bool SupportsCorrelationId => false;
    public int MaxFrameSize => _maxFrameSize;

    public MelsecSlmpCodec(int maxFrameSize = 4096)
    {
        _maxFrameSize = maxFrameSize;
    }

    /// <summary>
    /// Slmp3EFrame 요청을 바이트 스트림으로 인코딩합니다.
    /// </summary>
    public void Encode(Slmp3EFrame message, IBufferWriter<byte> output)
    {
        // 3E Request Frame 구조:
        // Subheader(2) + NetworkNo(1) + PcNo(1) + DestIo(2) + DestStation(1) + RequestDataLength(2) + MonitoringTimer(2) + Command(2) + Subcommand(2) + Data(N)
        // RequestDataLength = MonitoringTimer(2) + Command(2) + Subcommand(2) + Data.Length
        int reqDataLen = 6 + message.Data.Length;
        int totalLength = 9 + reqDataLen; // 2 + 1 + 1 + 2 + 1 + 2 + reqDataLen

        var span = output.GetSpan(totalLength);

        // Subheader (0x50, 0x00)
        span[0] = (byte)(message.Subheader & 0xFF);
        span[1] = (byte)((message.Subheader >> 8) & 0xFF);

        // Network identification
        span[2] = message.NetworkNo;
        span[3] = message.PcNo;
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(4, 2), message.DestModuleIo);
        span[6] = message.DestModuleStation;

        // Request Data Length
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(7, 2), (ushort)reqDataLen);

        // Monitoring Timer
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(9, 2), message.MonitoringTimer);

        // Command & Subcommand
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(11, 2), message.Command);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(13, 2), message.Subcommand);

        // Payload
        if (!message.Data.IsEmpty)
        {
            message.Data.Span.CopyTo(span.Slice(15, message.Data.Length));
        }

        output.Advance(totalLength);
    }

    /// <summary>
    /// 수신 바이트 버퍼에서 완성된 3E 응답 프레임을 추출합니다.
    /// </summary>
    public bool TryDecode(ref ReadOnlySequence<byte> buffer, out Slmp3EFrame message)
    {
        // 최소 헤더: Subheader(2) + Net(1) + PC(1) + DestIo(2) + Station(1) + ResponseDataLength(2) = 9 bytes
        if (buffer.Length < 9)
        {
            message = null!;
            return false;
        }

        Span<byte> header = stackalloc byte[9];
        buffer.Slice(0, 9).CopyTo(header);

        ushort responseDataLength = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(7, 2));
        int totalFrameSize = 9 + responseDataLength;

        if (buffer.Length < totalFrameSize)
        {
            message = null!;
            return false;
        }

        ushort subheader = (ushort)(header[0] | (header[1] << 8));
        byte netNo = header[2];
        byte pcNo = header[3];
        ushort destIo = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(4, 2));
        byte destStation = header[6];

        // 3E Response Body: EndCode(2 bytes, Little-Endian) + ResponseData(N)
        Span<byte> endCodeBytes = stackalloc byte[2];
        buffer.Slice(9, 2).CopyTo(endCodeBytes);
        ushort endCode = BinaryPrimitives.ReadUInt16LittleEndian(endCodeBytes);

        int dataLength = responseDataLength - 2; // EndCode(2) 차감
        ReadOnlyMemory<byte> responseData = ReadOnlyMemory<byte>.Empty;

        if (dataLength > 0)
        {
            var dataSeq = buffer.Slice(11, dataLength);
            responseData = dataSeq.ToArray();
        }

        message = new Slmp3EFrame(
            subheader: subheader,
            networkNo: netNo,
            pcNo: pcNo,
            destModuleIo: destIo,
            destModuleStation: destStation,
            monitoringTimerOrEndCode: endCode,
            command: 0,
            subcommand: 0,
            data: responseData);

        buffer = buffer.Slice(buffer.GetPosition(totalFrameSize));
        return true;
    }

    public string? ExtractCorrelationId(Slmp3EFrame message) => null;
    public bool IsAutonomousMessage(Slmp3EFrame message) => false;
}
