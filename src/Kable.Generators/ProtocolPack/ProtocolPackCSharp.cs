namespace Kable.Generators.ProtocolPack;

internal static class ProtocolPackCSharp
{
    public static string Literal(string? value)
    {
        var text = value ?? string.Empty;
        return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    public static string Interpolated(string template) => "$" + Literal(template);

    public static string HintName(string ns, string typeName)
    {
        var prefix = string.IsNullOrEmpty(ns) ? "Global" : ns.Replace('.', '_');
        return prefix + "_" + typeName + ".g.cs";
    }
}
