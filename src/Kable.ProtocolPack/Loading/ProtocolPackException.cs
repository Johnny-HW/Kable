using System;
using System.Collections.Generic;

namespace Kable.ProtocolPack.Loading;

/// <summary>
/// Thrown when a protocol folder cannot be parsed or fails validation. Nothing is applied.
/// </summary>
public sealed class ProtocolPackException : Exception
{
    public ProtocolPackException(string message)
        : this(new[] { message }, null)
    {
    }

    public ProtocolPackException(IReadOnlyList<string> errors)
        : this(errors, null)
    {
    }

    public ProtocolPackException(string message, Exception innerException)
        : this(new[] { message }, innerException)
    {
    }

    public ProtocolPackException(IReadOnlyList<string> errors, Exception? innerException)
        : base(Format(errors), innerException)
    {
        Errors = errors ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> Errors { get; }

    private static string Format(IReadOnlyList<string> errors)
    {
        if (errors == null || errors.Count == 0)
        {
            return "Invalid protocol pack.";
        }

        var parts = new string[errors.Count];
        for (var i = 0; i < errors.Count; i++)
        {
            parts[i] = errors[i];
        }

        return "Invalid protocol pack: " + string.Join("; ", parts);
    }
}
