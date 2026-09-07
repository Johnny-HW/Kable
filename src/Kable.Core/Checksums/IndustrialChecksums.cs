namespace Kable.Core.Checksums;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// 산업용 시리얼/소켓 통신 전반에서 널리 쓰이는 표준 체크섬 알고리즘 툴킷
/// (Modbus ASCII LRC, XOR BCC, Sum8, Two's Complement Sum)
/// </summary>
public static class IndustrialChecksums
{
    /// <summary>
    /// Modbus ASCII 규격 LRC(Longitudinal Redundancy Check)를 계산합니다.
    /// 모든 데이터 바이트의 합산 후 2의 보수(Negate)를 취합니다.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ComputeModbusLrc(ReadOnlySpan<byte> data)
    {
        byte sum = 0;
        for (int i = 0; i < data.Length; i++)
        {
            sum += data[i];
        }
        return (byte)-sum;
    }

    /// <summary>
    /// 바코드/RFID 리더, 계측 센서 통신에서 표준으로 쓰이는 XOR BCC (Block Check Character)를 계산합니다.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ComputeXorBcc(ReadOnlySpan<byte> data)
    {
        byte bcc = 0;
        for (int i = 0; i < data.Length; i++)
        {
            bcc ^= data[i];
        }
        return bcc;
    }

    /// <summary>
    /// 단순 바이트 누적 합산 (Sum8) 체크섬을 계산합니다.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ComputeSum8(ReadOnlySpan<byte> data)
    {
        byte sum = 0;
        for (int i = 0; i < data.Length; i++)
        {
            sum += data[i];
        }
        return sum;
    }

    /// <summary>
    /// 반도체 로봇 및 정밀 스테이지 ASCII 프로토콜에서 주로 사용되는 2의 보수 합산 체크섬을 계산합니다.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ComputeTwosComplementSum8(ReadOnlySpan<byte> data)
    {
        byte sum = ComputeSum8(data);
        return (byte)(~sum + 1);
    }
}
