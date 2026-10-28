using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Kable.ProtocolPack.Models;

/// <summary>
/// In-memory protocol folder: parent fields plus loaded commands and maps.
/// </summary>
public sealed class ProtocolPackDocument
{
    public int ProtocolPackVersion { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Codec { get; set; } = string.Empty;

    public EndpointSpec? Endpoint { get; set; }

    public int? UnitId { get; set; }

    public UnmatchedSpec? Unmatched { get; set; }

    public OutOfRangeSpec? OutOfRange { get; set; }

    public CsharpSpec? Csharp { get; set; }

    [YamlIgnore]
    public string FolderPath { get; set; } = string.Empty;

    [YamlIgnore]
    public IList<CommandSpec> Commands { get; set; } = new List<CommandSpec>();

    [YamlIgnore]
    public IList<MapSpec> Maps { get; set; } = new List<MapSpec>();
}
