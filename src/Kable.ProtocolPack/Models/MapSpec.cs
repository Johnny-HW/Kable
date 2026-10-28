using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Kable.ProtocolPack.Models;

/// <summary>
/// One Modbus or MELSEC map block. Scalar <see cref="Value"/> is rejected by the validator.
/// </summary>
public sealed class MapSpec
{
    public string Name { get; set; } = string.Empty;

    public string? Table { get; set; }

    public string? Code { get; set; }

    public int? Addr { get; set; }

    public int? Points { get; set; }

    public IList<object> Values { get; set; } = new List<object>();

    public MapRangeSpec? Range { get; set; }

    public object? Fill { get; set; }

    public object? Value { get; set; }

    [YamlIgnore]
    public bool HasScalarValue { get; set; }

    [YamlIgnore]
    public string SourcePath { get; set; } = string.Empty;
}

public sealed class MapRangeSpec
{
    public int Start { get; set; }

    public int Count { get; set; }
}
