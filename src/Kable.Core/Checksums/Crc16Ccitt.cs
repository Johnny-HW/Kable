namespace Kable.Core.Checksums;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// 256바이트 정적 룩업 테이블 기반 초고속 0-GC CRC-16 CCITT (XModem / 0x1021) 계산 엔진
/// 다항식: 0x1021, Init: 0x0000
/// 외산 웨이퍼 Aligner, 로봇 암 및 정밀 스테이지 프로토콜에 주로 사용됩니다.
/// </summary>
public static class Crc16Ccitt
{
    private static readonly ushort[] Table = new ushort[256];

    static Crc16Ccitt()
    {
        const ushort poly = 0x1021;
        for (int i = 0; i < 256; i++)
        {
            ushort temp = 0;
            ushort a = (ushort)(i << 8);
            for (int j = 0; j < 8; j++)
            {
                if (((temp ^ a) & 0x8000) != 0)
                {
                    temp = (ushort)((temp << 1) ^ poly);
                }
                else
                {
                    temp <<= 1;
                }
                a <<= 1;
            }
            Table[i] = temp;
        }
    }

    /// <summary>
    /// 지정된 바이트 시퀀스의 CRC-16 CCITT를 계산합니다.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Compute(ReadOnlySpan<byte> data, ushort init = 0x0000)
    {
        ushort crc = init;
        for (int i = 0; i < data.Length; i++)
        {
            byte index = (byte)((crc >> 8) ^ data[i]);
            crc = (ushort)((crc << 8) ^ Table[index]);
        }
        return crc;
    }

    /// <summary>
    /// CCITT CRC 2바이트(High, Low)가 포함된 수신 프레임의 무결성을 검증합니다.
    /// </summary>
    public static bool Validate(ReadOnlySpan<byte> frameWithCrc, ushort init = 0x0000)
    {
        if (frameWithCrc.Length < 3) return false;

        int payloadLength = frameWithCrc.Length - 2;
        ushort computed = Compute(frameWithCrc.Slice(0, payloadLength), init);
        ushort received = (ushort)((frameWithCrc[payloadLength] << 8) | frameWithCrc[payloadLength + 1]);
        return computed == received;
    }
}
