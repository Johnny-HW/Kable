namespace Kable.Configuration;

using System;

/// <summary>
/// Kable 장비 연결 전송 매체(Transport Medium) 강타입 열거형입니다.
/// </summary>
public enum KableTransportKind
{
    Unknown = 0,
    Tcp = 1,
    SerialPort = 2,
    NamedPipe = 3,
    Simulator = 4
}

/// <summary>
/// 설정 문자열을 <see cref="KableTransportKind"/> 강타입 열거형으로 변환하는 파서입니다.
/// </summary>
public static class KableTransportKindParser
{
    public static KableTransportKind Parse(string? transport)
    {
        if (string.IsNullOrWhiteSpace(transport))
        {
            return KableTransportKind.Unknown;
        }

        string normalized = transport!.Trim();

        if (string.Equals(normalized, "tcp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "socket", StringComparison.OrdinalIgnoreCase))
        {
            return KableTransportKind.Tcp;
        }

        if (string.Equals(normalized, "serialport", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "serial", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "rs232", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "rs485", StringComparison.OrdinalIgnoreCase))
        {
            return KableTransportKind.SerialPort;
        }

        if (string.Equals(normalized, "namedpipe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "pipe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "ipc", StringComparison.OrdinalIgnoreCase))
        {
            return KableTransportKind.NamedPipe;
        }

        if (string.Equals(normalized, "simulator", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "mock", StringComparison.OrdinalIgnoreCase))
        {
            return KableTransportKind.Simulator;
        }

        return KableTransportKind.Unknown;
    }
}
