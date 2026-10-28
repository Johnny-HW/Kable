namespace Kable.Generators.Tests.ProtocolPack;

using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

public class MapConstantsGenerationTests
{
    [Fact]
    public void ModbusMaps_EmitAddressAndLengthConstants()
    {
        var files = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(
                @"C:\packs\ABB-TALYS\protocol.kable.yaml",
                @"protocolPackVersion: 1
name: ABB-TALYS
codec: ModbusTcp
endpoint:
  transport: Tcp
  role: Server
  host: 127.0.0.1
  port: 502
unitId: 1
csharp:
  namespace: Cm.Abb.Protocol
"),
            new InMemoryAdditionalText(
                @"C:\packs\ABB-TALYS\maps\holding.kable.yaml",
                @"name: StatusBlock
table: holding
addr: 100
values:
  - 1
  - 0
  - 42
---
name: Spare
table: holding
range:
  start: 200
  count: 16
fill: 0
"));

        var run = PackGeneratorTestHost.Run(files);
        run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var sources = string.Join(Environment.NewLine, PackGeneratorTestHost.GeneratedSources(run));
        sources.Should().Contain("class ABBTALYSMaps");
        sources.Should().Contain("StatusBlockAddr = 100");
        sources.Should().Contain("StatusBlockLength = 3");
        sources.Should().Contain("SpareAddr = 200");
        sources.Should().Contain("SpareLength = 16");
        sources.Should().NotContain("ReadHolding");
        sources.Should().NotContain("WriteSingle");
    }

    [Fact]
    public void MelsecMaps_EmitCodeAndAddressConstants()
    {
        var files = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(
                @"C:\packs\Line-PLC\protocol.kable.yaml",
                @"protocolPackVersion: 1
name: Line-PLC
codec: MelsecSlmp3E
endpoint:
  transport: Tcp
  role: Server
  host: 127.0.0.1
  port: 5000
csharp:
  namespace: LinePlc.Protocol
"),
            new InMemoryAdditionalText(
                @"C:\packs\Line-PLC\maps\D.kable.yaml",
                @"name: DataBlock
code: D
addr: 1000
points: 8
values:
  - 0
  - 0
"));

        var run = PackGeneratorTestHost.Run(files);
        run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var sources = string.Join(Environment.NewLine, PackGeneratorTestHost.GeneratedSources(run));
        sources.Should().Contain("class LinePLCMaps");
        sources.Should().Contain("DataBlockAddr = 1000");
        sources.Should().Contain("DataBlockLength = 8");
        sources.Should().Contain("DataBlockCode = \"D\"");
    }
}
