namespace Kable.Generators.ProtocolPack;

using System;
using System.Text;

internal static class ProtocolPackIdentifiers
{
    public static string SanitizeTypeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "UnnamedPack";
        }

        var builder = new StringBuilder(name!.Length);
        var upperNext = true;
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
                upperNext = false;
            }
            else
            {
                upperNext = true;
            }
        }

        if (builder.Length == 0)
        {
            return "UnnamedPack";
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    public static string ToPascalIdentifier(string? name) => SanitizeTypeName(name);

    public static string EnsurePartial(string typeKind)
    {
        var stripped = StripPublic(typeKind);
        if (stripped.IndexOf("partial", StringComparison.Ordinal) >= 0)
        {
            return stripped;
        }

        return "partial " + stripped;
    }

    public static string StripPublic(string typeKind)
    {
        const string prefix = "public ";
        if (typeKind.StartsWith(prefix, StringComparison.Ordinal))
        {
            return typeKind.Substring(prefix.Length).Trim();
        }

        return typeKind.Trim();
    }
}
