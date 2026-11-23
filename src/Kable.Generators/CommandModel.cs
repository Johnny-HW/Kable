namespace Kable.Generators;

using System;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

internal readonly struct ContainingTypeInfo : IEquatable<ContainingTypeInfo>
{
    public string TypeKind { get; }
    public string DeclarationName { get; }
    public bool IsPartial { get; }

    public ContainingTypeInfo(string typeKind, string declarationName, bool isPartial)
    {
        TypeKind = typeKind;
        DeclarationName = declarationName;
        IsPartial = isPartial;
    }

    public bool Equals(ContainingTypeInfo other)
        => string.Equals(TypeKind, other.TypeKind, StringComparison.Ordinal)
           && string.Equals(DeclarationName, other.DeclarationName, StringComparison.Ordinal)
           && IsPartial == other.IsPartial;

    public override bool Equals(object? obj) => obj is ContainingTypeInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(TypeKind);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(DeclarationName);
            hash = (hash * 31) + (IsPartial ? 1 : 0);
            return hash;
        }
    }
}

internal readonly struct CommandDiagnosticLocation : IEquatable<CommandDiagnosticLocation>
{
    public string FilePath { get; }
    public TextSpan TextSpan { get; }
    public LinePositionSpan LineSpan { get; }

    public CommandDiagnosticLocation(string filePath, TextSpan textSpan, LinePositionSpan lineSpan)
    {
        FilePath = filePath;
        TextSpan = textSpan;
        LineSpan = lineSpan;
    }

    public Location ToLocation()
        => string.IsNullOrEmpty(FilePath)
            ? Location.None
            : Location.Create(FilePath, TextSpan, LineSpan);

    public bool Equals(CommandDiagnosticLocation other)
        => string.Equals(FilePath, other.FilePath, StringComparison.Ordinal)
           && TextSpan.Equals(other.TextSpan)
           && LineSpan.Equals(other.LineSpan);

    public override bool Equals(object? obj) => obj is CommandDiagnosticLocation other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(FilePath);
            hash = (hash * 31) + TextSpan.GetHashCode();
            hash = (hash * 31) + LineSpan.GetHashCode();
            return hash;
        }
    }
}

internal sealed class CommandModel : IEquatable<CommandModel>
{
    public string Namespace { get; }
    public ImmutableArray<ContainingTypeInfo> ContainingTypes { get; }
    public string TypeName { get; }
    public string TypeParameters { get; }
    public string TypeKind { get; }
    public string Template { get; }
    public bool IsUrgent { get; }
    public bool IsPartial { get; }
    public string HintName { get; }
    public CommandDiagnosticLocation Location { get; }
    public ImmutableArray<string> Properties { get; }

    public CommandModel(
        string @namespace,
        ImmutableArray<ContainingTypeInfo> containingTypes,
        string typeName,
        string typeParameters,
        string typeKind,
        string template,
        bool isUrgent,
        bool isPartial,
        string hintName,
        CommandDiagnosticLocation location,
        ImmutableArray<string> properties)
    {
        Namespace = @namespace;
        ContainingTypes = containingTypes;
        TypeName = typeName;
        TypeParameters = typeParameters;
        TypeKind = typeKind;
        Template = template;
        IsUrgent = isUrgent;
        IsPartial = isPartial;
        HintName = hintName;
        Location = location;
        Properties = properties;
    }

    public bool Equals(CommandModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
               && string.Equals(TypeName, other.TypeName, StringComparison.Ordinal)
               && string.Equals(TypeParameters, other.TypeParameters, StringComparison.Ordinal)
               && string.Equals(TypeKind, other.TypeKind, StringComparison.Ordinal)
               && string.Equals(Template, other.Template, StringComparison.Ordinal)
               && IsUrgent == other.IsUrgent
               && IsPartial == other.IsPartial
               && string.Equals(HintName, other.HintName, StringComparison.Ordinal)
               && Location.Equals(other.Location)
               && SequenceEqual(ContainingTypes, other.ContainingTypes)
               && SequenceEqual(Properties, other.Properties);
    }

    public override bool Equals(object? obj) => Equals(obj as CommandModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(Namespace);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(TypeName);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(TypeParameters);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(TypeKind);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Template);
            hash = (hash * 31) + (IsUrgent ? 1 : 0);
            hash = (hash * 31) + (IsPartial ? 1 : 0);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(HintName);
            hash = (hash * 31) + Location.GetHashCode();
            for (int i = 0; i < ContainingTypes.Length; i++)
            {
                hash = (hash * 31) + ContainingTypes[i].GetHashCode();
            }
            for (int i = 0; i < Properties.Length; i++)
            {
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Properties[i]);
            }
            return hash;
        }
    }

    private static bool SequenceEqual<T>(ImmutableArray<T> left, ImmutableArray<T> right) where T : IEquatable<T>
    {
        if (left.IsDefault && right.IsDefault) return true;
        if (left.IsDefault || right.IsDefault || left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++)
        {
            if (!left[i].Equals(right[i])) return false;
        }
        return true;
    }
}

internal static class CommandTemplateFormatter
{
    public static string GenerateFormatExpression(string template)
    {
        if (string.IsNullOrEmpty(template)) return "\"\"";

        if (template.IndexOf('{') < 0)
        {
            return $"\"{EscapeLiteral(template)}\"";
        }

        var escaped = EscapeInterpolatedTemplate(template);
        return $"global::System.FormattableString.Invariant($\"{escaped}\")";
    }

    private static string EscapeLiteral(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        for (int i = 0; i < text.Length; i++)
        {
            AppendEscapedChar(sb, text[i]);
        }
        return sb.ToString();
    }

    private static string EscapeInterpolatedTemplate(string template)
    {
        var sb = new StringBuilder(template.Length + 8);
        int i = 0;
        bool inHole = false;

        while (i < template.Length)
        {
            char c = template[i];
            if (!inHole)
            {
                if (c == '{')
                {
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        sb.Append("{{");
                        i += 2;
                        continue;
                    }
                    inHole = true;
                    sb.Append('{');
                    i++;
                    continue;
                }

                if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
                {
                    sb.Append("}}");
                    i += 2;
                    continue;
                }

                AppendEscapedChar(sb, c);
                i++;
            }
            else
            {
                if (c == '}')
                {
                    inHole = false;
                }
                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }

    private static void AppendEscapedChar(StringBuilder sb, char c)
    {
        switch (c)
        {
            case '\\': sb.Append(@"\\"); break;
            case '"': sb.Append("\\\""); break;
            case '\r': sb.Append(@"\r"); break;
            case '\n': sb.Append(@"\n"); break;
            case '\t': sb.Append(@"\t"); break;
            case '\0': sb.Append(@"\0"); break;
            default: sb.Append(c); break;
        }
    }
}
