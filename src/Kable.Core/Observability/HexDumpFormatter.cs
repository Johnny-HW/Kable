namespace Kable.Observability;

using System;
using System.Text;

/// <summary>
/// 바이트 패킷을 Wireshark / 터미널 스타일 헥사 덤프로 포맷팅하는 유틸리티
/// </summary>
public static class HexDumpFormatter
{
    public static string Format(ReadOnlySpan<byte> bytes, int bytesPerLine = 16)
    {
        if (bytes.IsEmpty) return string.Empty;
        if (bytesPerLine <= 0) bytesPerLine = 16;

        var sb = new StringBuilder(bytes.Length * 4);
        int length = bytes.Length;

        for (int i = 0; i < length; i += bytesPerLine)
        {
            // Offset
            sb.AppendFormat("{0:X4}: ", i);

            int chunk = Math.Min(bytesPerLine, length - i);

            // Hex Bytes
            for (int j = 0; j < bytesPerLine; j++)
            {
                if (j < chunk)
                {
                    sb.AppendFormat("{0:X2} ", bytes[i + j]);
                }
                else
                {
                    sb.Append("   ");
                }
                if (j == 7) sb.Append(' ');
            }

            sb.Append(" |");

            // ASCII representation
            for (int j = 0; j < chunk; j++)
            {
                byte b = bytes[i + j];
                char c = (b >= 32 && b <= 126) ? (char)b : '.';
                sb.Append(c);
            }

            sb.Append('|');
            if (i + bytesPerLine < length)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    public static string Format(byte[]? bytes, int bytesPerLine = 16)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;
        return Format(bytes.AsSpan(), bytesPerLine);
    }
}
