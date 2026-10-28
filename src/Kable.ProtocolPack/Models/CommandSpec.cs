using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Kable.ProtocolPack.Models;

/// <summary>
/// AsciiLine command file. <see cref="Codec"/> and <see cref="Endpoint"/> are forbidden on disk.
/// </summary>
public sealed class CommandSpec
{
    public string Name { get; set; } = string.Empty;

    public string Match { get; set; } = string.Empty;

    public bool Urgent { get; set; }

    public IList<ParamSpec> Params { get; set; } = new List<ParamSpec>();

    public OutcomeSpec? Ok { get; set; }

    public IList<ErrorOutcomeSpec> Errors { get; set; } = new List<ErrorOutcomeSpec>();

    public string? Codec { get; set; }

    public EndpointSpec? Endpoint { get; set; }

    [YamlIgnore]
    public string SourcePath { get; set; } = string.Empty;
}
