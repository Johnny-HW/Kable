using System;

namespace Kable.ProtocolPack.Models;

/// <summary>
/// Allowed <c>codec</c> values. Keep aligned with SimPort SimProtocolKind / PROTOCOL_PACK.md.
/// </summary>
public static class ProtocolPackCodecs
{
    public const string AsciiLine = "AsciiLine";
    public const string ModbusRtu = "ModbusRtu";
    public const string ModbusTcp = "ModbusTcp";
    public const string MelsecSlmp3E = "MelsecSlmp3E";

    public static readonly string[] All =
    {
        AsciiLine,
        ModbusRtu,
        ModbusTcp,
        MelsecSlmp3E,
    };

    public static bool IsKnown(string? codec)
    {
        if (string.IsNullOrEmpty(codec))
        {
            return false;
        }

        for (var i = 0; i < All.Length; i++)
        {
            if (string.Equals(All[i], codec, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsAsciiLine(string? codec) =>
        string.Equals(codec, AsciiLine, StringComparison.Ordinal);

    public static bool IsModbus(string? codec) =>
        string.Equals(codec, ModbusRtu, StringComparison.Ordinal) ||
        string.Equals(codec, ModbusTcp, StringComparison.Ordinal);

    public static bool IsMelsec(string? codec) =>
        string.Equals(codec, MelsecSlmp3E, StringComparison.Ordinal);
}

/// <summary>
/// Allowed MELSEC <c>code</c> names. Keep synchronized with Kable.Melsec.Protocol.MelsecDeviceCode.
/// </summary>
public static class MelsecDeviceCodeNames
{
    public static readonly string[] All =
    {
        "D", "W", "R", "ZR", "M", "X", "Y", "L", "F", "B",
    };

    public static bool IsKnown(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        for (var i = 0; i < All.Length; i++)
        {
            if (string.Equals(All[i], code, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
