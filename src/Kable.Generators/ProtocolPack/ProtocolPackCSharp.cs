namespace Kable.Generators.ProtocolPack;

using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

internal static class ProtocolPackCSharp
{
    public static string Literal(string? value)
    {
        var text = value ?? string.Empty;
        var sb = new StringBuilder(text.Length + 2);
        sb.Append('"');
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\0': sb.Append("\\0"); break;
                default:
                    if (ch < 0x20)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    public static string Interpolated(string template)
        => "global::System.FormattableString.Invariant($" + Literal(template) + ")";

    public static string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return "_";
        }

        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
            ? "@" + identifier
            : identifier;
    }

    public static string HintName(string ns, string typeName)
    {
        var prefix = string.IsNullOrEmpty(ns) ? "Global" : ns.Replace('.', '_');
        return prefix + "_" + typeName + ".g.cs";
    }
}
