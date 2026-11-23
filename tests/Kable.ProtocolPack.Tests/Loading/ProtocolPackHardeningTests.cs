using System;
using System.Collections.Generic;
using System.IO;
using Kable.ProtocolPack.Loading;
using Kable.ProtocolPack.Models;
using Xunit;

namespace Kable.ProtocolPack.Tests.Loading;

public sealed class ProtocolPackHardeningTests
{
    [Theory]
    [InlineData("../outside.kable.yaml")]
    [InlineData(@"..\..\outside.kable.yaml")]
    public void Save_RejectsPathTraversalInSourcePath(string traversal)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kable-pp-sec-" + Guid.NewGuid().ToString("N"));
        try
        {
            var doc = new ProtocolPackDocument
            {
                ProtocolPackVersion = 1,
                Name = "TestPack",
                Codec = "AsciiLine",
                Commands = new List<CommandSpec>
                {
                    new CommandSpec { Name = "Ping", Match = "PING", SourcePath = traversal }
                }
            };

            Assert.Throws<ProtocolPackException>(() => ProtocolPackLoader.Save(tempDir, doc));
            Assert.False(File.Exists(Path.GetFullPath(Path.Combine(tempDir, traversal))));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_RejectsRootedSourcePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kable-pp-root-" + Guid.NewGuid().ToString("N"));
        var rootedTarget = Path.Combine(Path.GetTempPath(), "kable-evil-" + Guid.NewGuid().ToString("N") + ".yaml");
        try
        {
            var doc = new ProtocolPackDocument
            {
                ProtocolPackVersion = 1,
                Name = "TestPack",
                Codec = "AsciiLine",
                Commands = new List<CommandSpec>
                {
                    new CommandSpec { Name = "Ping", Match = "PING", SourcePath = rootedTarget }
                }
            };

            Assert.Throws<ProtocolPackException>(() => ProtocolPackLoader.Save(tempDir, doc));
            Assert.False(File.Exists(rootedTarget));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            if (File.Exists(rootedTarget)) File.Delete(rootedTarget);
        }
    }

    [Fact]
    public void Load_HandlesHex64BitAndFloatMapValuesWithoutOverflow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kable-pp-vals-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "maps"));
            File.WriteAllText(Path.Combine(tempDir, "protocol.kable.yaml"),
                "protocolPackVersion: 1\nname: ValPack\ncodec: ModbusTcp\n");
            File.WriteAllText(Path.Combine(tempDir, "maps", "m.kable.yaml"),
                "name: Mixed\ntable: holding\naddr: 10\nvalues:\n  - \"0x1A\"\n  - 5000000000\n  - 3.5\n  - true\n");

            var pack = ProtocolPackLoader.Load(tempDir);
            var vals = pack.Maps[0].Values;
            Assert.Equal(26, vals[0]);
            Assert.Equal(5000000000L, vals[1]);
            Assert.Equal(3.5d, vals[2]);
            Assert.Equal(true, vals[3]);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_ChecksUnderscorePlaceholdersDuplicateNamesBoundsAndCodeInjection()
    {
        var doc = new ProtocolPackDocument
        {
            ProtocolPackVersion = 1,
            Name = "BadPack",
            Codec = "MelsecSlmp3E",
            Endpoint = new EndpointSpec { Port = 70000, BaudRate = -1 },
            Csharp = new CsharpSpec { Namespace = "Bad;Namespace", TypeKind = "class Evil { } //" },
            Commands = new List<CommandSpec>
            {
                new CommandSpec
                {
                    Name = "Run",
                    Match = "CMD.{batch_id}",
                    Params = new List<ParamSpec> { new ParamSpec { Name = "other", Type = "dynamic" } }
                },
                new CommandSpec { Name = "Run", Match = "CMD2" }
            },
            Maps = new List<MapSpec>
            {
                new MapSpec
                {
                    Name = "BadMap",
                    Code = "D",
                    Addr = -5,
                    Points = 0,
                    Range = new MapRangeSpec { Start = -1, Count = 0 },
                    HasScalarValue = true,
                    Value = 1
                }
            }
        };

        var errors = ProtocolPackValidator.Validate(doc);
        Assert.Contains(errors, e => e.Contains("batch_id", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("Duplicate command name", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("Endpoint port", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("csharp.typeKind", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("csharp.namespace", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("unsupported type 'dynamic'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("MELSEC map", StringComparison.Ordinal) && e.Contains("scalar 'value'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("out-of-range addr", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("points must be positive", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("invalid range.start/count", StringComparison.Ordinal));
    }

    [Fact]
    public void DeserializeMapDocuments_SinglePassTracksScalarValueAcrossMultiDocs()
    {
        var yaml = "name: First\ntable: holding\naddr: 1\nvalues:\n  - 10\n---\n---\nname: Second\ntable: holding\naddr: 2\nvalue: 99\n";
        var maps = ProtocolPackYaml.DeserializeMapDocuments(yaml);

        Assert.Equal(2, maps.Count);
        Assert.False(maps[0].HasScalarValue);
        Assert.True(maps[1].HasScalarValue);
        Assert.NotNull(maps[1].Value);
    }
}
