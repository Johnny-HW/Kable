namespace Kable.Generators.Tests.ProtocolPack;

using System;
using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

public sealed class ProtocolPackHardeningGenerationTests
{
    [Fact]
    public void RootLevelPack_BindsCommandsAndMapsWithoutOrphans()
    {
        var files = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(
                "protocol.kable.yaml",
                "protocolPackVersion: 1\nname: RootPack\ncodec: AsciiLine\ncsharp:\n  namespace: Root.Protocol\n"),
            new InMemoryAdditionalText(
                "commands/ping.kable.yaml",
                "name: Ping\nmatch: \"PING\\t{class}\"\nparams:\n  - name: class\n    type: string\nok:\n  replies:\n    - ACK\nerrors:\n  - name: Err2\n  - name: Err\n  - name: Err\n"));

        var run = PackGeneratorTestHost.Run(files);
        run.Diagnostics.Should().BeEmpty();

        var assembly = PackGeneratorTestHost.Emit(run);
        var cmdType = assembly.GetType("Root.Protocol.PingCommand");
        cmdType.Should().NotBeNull();
        var cmd = Activator.CreateInstance(cmdType!, "A");
        var wire = (string)cmdType!.GetMethod("FormatWireMessage")!.Invoke(cmd, null)!;
        wire.Should().Be("PING\tA");

        var replyType = assembly.GetType("Root.Protocol.PingReply");
        replyType.Should().NotBeNull();
        Enum.GetNames(replyType!).Should().Contain(new[] { "Ok", "Err2", "Err", "Err3", "Unmatched" });
    }

    [Fact]
    public void MapConstants_UniqueLoopResolvesPreExistingSuffixedCollision()
    {
        var files = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(
                @"C:\packs\DupMaps\protocol.kable.yaml",
                "protocolPackVersion: 1\nname: DupMaps\ncodec: ModbusTcp\ncsharp:\n  namespace: Dup.Protocol\n"),
            new InMemoryAdditionalText(
                @"C:\packs\DupMaps\maps\m.kable.yaml",
                "name: Foo3\ntable: holding\naddr: 1\nvalues: [1]\n---\nname: Foo\ntable: holding\naddr: 2\nvalues: [1]\n---\nname: Foo\ntable: holding\naddr: 3\nvalues: [1]\n"));

        var run = PackGeneratorTestHost.Run(files);
        run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var assembly = PackGeneratorTestHost.Emit(run);
        var mapsType = assembly.GetType("Dup.Protocol.DupMapsMaps");
        mapsType.Should().NotBeNull();
        mapsType!.GetField("Foo3Addr")!.GetRawConstantValue().Should().Be(1);
        mapsType.GetField("FooAddr")!.GetRawConstantValue().Should().Be(2);
        mapsType.GetField("Foo4Addr")!.GetRawConstantValue().Should().Be(3);
    }

    [Fact]
    public void TypeKindCodeInjection_IsRejectedByValidationDiagnostic()
    {
        var files = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(
                @"C:\packs\Inject\protocol.kable.yaml",
                "protocolPackVersion: 1\nname: Inject\ncodec: AsciiLine\ncsharp:\n  namespace: Inj.Protocol\n  typeKind: \"class Bad {} //\"\n"),
            new InMemoryAdditionalText(
                @"C:\packs\Inject\commands\a.kable.yaml",
                "name: A\nmatch: A\n"));

        var run = PackGeneratorTestHost.Run(files);
        run.Diagnostics.Should().Contain(d => d.Severity == DiagnosticSeverity.Error && d.GetMessage().Contains("typeKind"));
    }
}
