namespace Kable.Melsec.Codecs;

using System;
using System.Buffers;
using System.Buffers.Binary;
using Kable.Codecs;
using Kable.Melsec.Protocol;

/// <summary>
/// 미쓰비시 SLMP / MC Protocol 3E 바이너리 프레임 코덱.
/// 요청(0x0050, Command/Subcommand)과 응답(0x00D0, EndCode)을 같은 프레임 레이아웃으로 인코드/디코드합니다.
/// </summary>
public sealed class MelsecSlmpCodec : IProtocolCodec<Slmp3EFrame>
{
    private const ushort RequestSubheader = 0x0050;
    private const ushort ResponseSubheader = 0x00D0;
    private const int AccessRouteLength = 9;
    private const int RequestCommandHeaderLength = 6;
    private const int ResponseEndCodeLength = 2;

    private readonly int _maxFrameSize;

    // MC Protocol 3E TCP 규격은 요청 헤더에 트랜잭션 ID가 기본 내장되지 않고 FIFO 1:1 트랜잭션으로 동작
    public bool SupportsCorrelationId => false;
    public int MaxFrameSize => _maxFrameSize;

    public MelsecSlmpCodec(int maxFrameSize = 4096)
    {
        _maxFrameSize = maxFrameSize;
    }

    /// <summary>
    /// Slmp3EFrame을 바이트 스트림으로 인코딩합니다. 0x00D0은 EndCode 응답 바디, 그 외는 요청 바디입니다.
    /// </summary>
    public void Encode(Slmp3EFrame message, IBufferWriter<byte> output)
    {
        if (message.Subheader == ResponseSubheader)
        {
            EncodeResponse(message, output);
            return;
        }

        EncodeRequest(message, output);
    }

    /// <summary>
    /// 수신 바이트 버퍼에서 완성된 3E 요청(0x0050) 또는 응답(0x00D0) 프레임을 추출합니다.
    /// </summary>
    public bool TryDecode(ref ReadOnlySequence<byte> buffer, out Slmp3EFrame message)
    {
        if (buffer.Length < AccessRouteLength)
        {
            message = null!;
            return false;
        }

        Span<byte> header = stackalloc byte[AccessRouteLength];
        buffer.Slice(0, AccessRouteLength).CopyTo(header);

        ushort subheader = (ushort)(header[0] | (header[1] << 8));
        ushort lengthField = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(7, 2));
        int totalFrameSize = AccessRouteLength + lengthField;
        int minBody = subheader == RequestSubheader ? RequestCommandHeaderLength : ResponseEndCodeLength;

        if (totalFrameSize < AccessRouteLength + minBody || totalFrameSize > _maxFrameSize)
        {
            buffer = buffer.Slice(1);
            message = null!;
            return false;
        }

        if (buffer.Length < totalFrameSize)
        {
            message = null!;
            return false;
        }

        byte netNo = header[2];
        byte pcNo = header[3];
        ushort destIo = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(4, 2));
        byte destStation = header[6];

        if (subheader == RequestSubheader)
        {
            message = DecodeRequestBody(buffer, netNo, pcNo, destIo, destStation, lengthField);
        }
        else
        {
            message = DecodeResponseBody(buffer, subheader, netNo, pcNo, destIo, destStation, lengthField);
        }

        buffer = buffer.Slice(buffer.GetPosition(totalFrameSize));
        return true;
    }

    public string? ExtractCorrelationId(Slmp3EFrame message) => null;
    public bool IsAutonomousMessage(Slmp3EFrame message) => false;

    private static void EncodeRequest(Slmp3EFrame message, IBufferWriter<byte> output)
    {
        // 3E Request: Subheader(2)+Net(1)+Pc(1)+DestIo(2)+Station(1)+Len(2)+Timer(2)+Command(2)+Subcommand(2)+Data(N)
        int reqDataLen = RequestCommandHeaderLength + message.Data.Length;
        int totalLength = AccessRouteLength + reqDataLen;
        var span = output.GetSpan(totalLength);

        WriteAccessRoute(span, message, (ushort)reqDataLen);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(9, 2), message.MonitoringTimer);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(11, 2), message.Command);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(13, 2), message.Subcommand);

        if (!message.Data.IsEmpty)
        {
            message.Data.Span.CopyTo(span.Slice(15, message.Data.Length));
        }

        output.Advance(totalLength);
    }

    private static void EncodeResponse(Slmp3EFrame message, IBufferWriter<byte> output)
    {
        // 3E Response: Subheader(2)+Net(1)+Pc(1)+DestIo(2)+Station(1)+Len(2)+EndCode(2)+Data(N)
        int respDataLen = ResponseEndCodeLength + message.Data.Length;
        int totalLength = AccessRouteLength + respDataLen;
        var span = output.GetSpan(totalLength);

        WriteAccessRoute(span, message, (ushort)respDataLen);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(9, 2), message.MonitoringTimer);

        if (!message.Data.IsEmpty)
        {
            message.Data.Span.CopyTo(span.Slice(11, message.Data.Length));
        }

        output.Advance(totalLength);
    }

    private static void WriteAccessRoute(Span<byte> span, Slmp3EFrame message, ushort lengthField)
    {
        span[0] = (byte)(message.Subheader & 0xFF);
        span[1] = (byte)((message.Subheader >> 8) & 0xFF);
        span[2] = message.NetworkNo;
        span[3] = message.PcNo;
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(4, 2), message.DestModuleIo);
        span[6] = message.DestModuleStation;
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(7, 2), lengthField);
    }

    private static Slmp3EFrame DecodeRequestBody(
        ReadOnlySequence<byte> buffer,
        byte netNo,
        byte pcNo,
        ushort destIo,
        byte destStation,
        ushort lengthField)
    {
        Span<byte> commandHeader = stackalloc byte[RequestCommandHeaderLength];
        buffer.Slice(AccessRouteLength, RequestCommandHeaderLength).CopyTo(commandHeader);

        ushort timer = BinaryPrimitives.ReadUInt16LittleEndian(commandHeader.Slice(0, 2));
        ushort command = BinaryPrimitives.ReadUInt16LittleEndian(commandHeader.Slice(2, 2));
        ushort subcommand = BinaryPrimitives.ReadUInt16LittleEndian(commandHeader.Slice(4, 2));

        int dataLength = lengthField - RequestCommandHeaderLength;
        ReadOnlyMemory<byte> data = CopyPayload(buffer, AccessRouteLength + RequestCommandHeaderLength, dataLength);

        return new Slmp3EFrame(
            subheader: RequestSubheader,
            networkNo: netNo,
            pcNo: pcNo,
            destModuleIo: destIo,
            destModuleStation: destStation,
            monitoringTimerOrEndCode: timer,
            command: command,
            subcommand: subcommand,
            data: data);
    }

    private static Slmp3EFrame DecodeResponseBody(
        ReadOnlySequence<byte> buffer,
        ushort subheader,
        byte netNo,
        byte pcNo,
        ushort destIo,
        byte destStation,
        ushort lengthField)
    {
        Span<byte> endCodeBytes = stackalloc byte[ResponseEndCodeLength];
        buffer.Slice(AccessRouteLength, ResponseEndCodeLength).CopyTo(endCodeBytes);
        ushort endCode = BinaryPrimitives.ReadUInt16LittleEndian(endCodeBytes);

        int dataLength = lengthField - ResponseEndCodeLength;
        ReadOnlyMemory<byte> data = CopyPayload(buffer, AccessRouteLength + ResponseEndCodeLength, dataLength);

        return new Slmp3EFrame(
            subheader: subheader,
            networkNo: netNo,
            pcNo: pcNo,
            destModuleIo: destIo,
            destModuleStation: destStation,
            monitoringTimerOrEndCode: endCode,
            command: 0,
            subcommand: 0,
            data: data);
    }

    private static ReadOnlyMemory<byte> CopyPayload(ReadOnlySequence<byte> buffer, int start, int dataLength)
    {
        if (dataLength <= 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        return buffer.Slice(start, dataLength).ToArray();
    }
}
