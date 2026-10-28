namespace Kable.Generators.ProtocolPack;

using System;
using Microsoft.CodeAnalysis;

internal readonly struct AdditionalPackFile : IEquatable<AdditionalPackFile>
{
    public AdditionalPackFile(string path, string text)
    {
        Path = path ?? string.Empty;
        Text = text ?? string.Empty;
    }

    public string Path { get; }

    public string Text { get; }

    public bool Equals(AdditionalPackFile other) =>
        string.Equals(Path, other.Path, StringComparison.Ordinal) &&
        string.Equals(Text, other.Text, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AdditionalPackFile other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (Path.GetHashCode() * 397) ^ Text.GetHashCode();
        }
    }

    public static AdditionalPackFile Capture(AdditionalText text, System.Threading.CancellationToken cancellationToken)
    {
        var source = text.GetText(cancellationToken);
        return new AdditionalPackFile(text.Path, source == null ? string.Empty : source.ToString());
    }
}
