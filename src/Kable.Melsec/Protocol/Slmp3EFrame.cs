namespace Kable.Melsec.Protocol;

using System;
using System.Buffers.Binary;

/// <summary>
/// SLMP / MC Protocol 3E 바이너리 프레임
/// </summary>
public sealed class Slmp3EFrame
{
    public ushort Subheader { get; }      // 0x0050 (Request), 0x00D0 (Response)
    public byte NetworkNo { get; }        // 보통 0x00
    public byte PcNo { get; }             // 보통 0xFF
    public ushort DestModuleIo { get; }   // 보통 0x03FF
    public byte DestModuleStation { get; }// 보통 0x00

    public ushort MonitoringTimer { get; }// 요청 시: CPU 대기 시간 (단위: 250ms), 응답 시: EndCode(종료 코드)
    public ushort Command { get; }        // 0x0401 (Read), 0x1401 (Write)
    public ushort Subcommand { get; }     // 0x0000 (Word 단위), 0x0001 (Bit 단위)
    public ReadOnlyMemory<byte> Data { get; }

    public bool IsSuccess => EndCode == 0;
    public ushort EndCode => Subheader == 0x00D0 ? MonitoringTimer : (ushort)0;

    public Slmp3EFrame(
        ushort subheader,
        byte networkNo,
        byte pcNo,
        ushort destModuleIo,
        byte destModuleStation,
        ushort monitoringTimerOrEndCode,
        ushort command,
        ushort subcommand,
        ReadOnlyMemory<byte> data)
    {
        Subheader = subheader;
        NetworkNo = networkNo;
        PcNo = pcNo;
        DestModuleIo = destModuleIo;
        DestModuleStation = destModuleStation;
        MonitoringTimer = monitoringTimerOrEndCode;
        Command = command;
        Subcommand = subcommand;
        Data = data;
    }

    /// <summary>
    /// 디바이스 일괄 읽기(Batch Read: 0x0401) 요청 프레임 생성
    /// </summary>
    public static Slmp3EFrame CreateReadDeviceRequest(
        MelsecDeviceCode device,
        int headDeviceNo,
        ushort devicePoints,
        bool isBitAccess = false,
        byte networkNo = 0,
        byte pcNo = 0xFF,
        ushort timer = 16)
    {
        // Request Data Format: HeadDeviceNo(3 bytes, Little-Endian) + DeviceCode(1 byte) + DevicePoints(2 bytes, Little-Endian) = 6 bytes
        byte[] data = new byte[6];
        data[0] = (byte)(headDeviceNo & 0xFF);
        data[1] = (byte)((headDeviceNo >> 8) & 0xFF);
        data[2] = (byte)((headDeviceNo >> 16) & 0xFF);
        data[3] = (byte)device;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4, 2), devicePoints);

        return new Slmp3EFrame(
            subheader: 0x0050,
            networkNo: networkNo,
            pcNo: pcNo,
            destModuleIo: 0x03FF,
            destModuleStation: 0,
            monitoringTimerOrEndCode: timer,
            command: 0x0401,
            subcommand: isBitAccess ? (ushort)0x0001 : (ushort)0x0000,
            data: data);
    }

    /// <summary>
    /// 워드 디바이스 일괄 쓰기(Batch Write: 0x1401) 요청 프레임 생성
    /// </summary>
    public static Slmp3EFrame CreateWriteWordsRequest(
        MelsecDeviceCode device,
        int headDeviceNo,
        ReadOnlySpan<ushort> values,
        byte networkNo = 0,
        byte pcNo = 0xFF,
        ushort timer = 16)
    {
        // Request Data: HeadDeviceNo(3) + DeviceCode(1) + DevicePoints(2) + WriteData(points * 2)
        ushort points = (ushort)values.Length;
        byte[] data = new byte[6 + (points * 2)];

        data[0] = (byte)(headDeviceNo & 0xFF);
        data[1] = (byte)((headDeviceNo >> 8) & 0xFF);
        data[2] = (byte)((headDeviceNo >> 16) & 0xFF);
        data[3] = (byte)device;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4, 2), points);

        for (int i = 0; i < points; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6 + (i * 2), 2), values[i]);
        }

        return new Slmp3EFrame(
            subheader: 0x0050,
            networkNo: networkNo,
            pcNo: pcNo,
            destModuleIo: 0x03FF,
            destModuleStation: 0,
            monitoringTimerOrEndCode: timer,
            command: 0x1401,
            subcommand: 0x0000, // Word unit
            data: data);
    }
}
