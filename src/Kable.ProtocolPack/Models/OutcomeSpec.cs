using System.Collections.Generic;

namespace Kable.ProtocolPack.Models;

public sealed class OutcomeSpec
{
    public IList<string> Replies { get; set; } = new List<string>();

    public int? Exception { get; set; }

    public object? EndCode { get; set; }
}

public sealed class ErrorOutcomeSpec
{
    public string Name { get; set; } = string.Empty;

    public IList<string> Replies { get; set; } = new List<string>();
}

public sealed class UnmatchedSpec
{
    public IList<string> Replies { get; set; } = new List<string>();

    public int? Exception { get; set; }

    public object? EndCode { get; set; }
}

public sealed class OutOfRangeSpec
{
    public int? Exception { get; set; }

    public object? EndCode { get; set; }
}

public sealed class ParamSpec
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "string";
}

public sealed class CsharpSpec
{
    public string? Namespace { get; set; }

    public string? TypeKind { get; set; }
}
