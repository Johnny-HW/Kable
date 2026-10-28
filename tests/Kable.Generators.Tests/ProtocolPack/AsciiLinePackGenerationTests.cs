namespace Kable.Generators.Tests.ProtocolPack;

using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

public class AsciiLinePackGenerationTests
{
    [Fact]
    public void MassHunter_IgnitePlasma_FormatsWireMessageAsOpon()
    {
        var run = PackGeneratorTestHost.Run(PackGeneratorTestHost.LoadMassHunter());
        run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var assembly = PackGeneratorTestHost.Emit(run);
        var type = assembly.GetType("Icpms.MassHunter.Protocol.IgnitePlasmaCommand");
        type.Should().NotBeNull();

        var command = Activator.CreateInstance(type!);
        var formatted = type!.GetMethod("FormatWireMessage")!.Invoke(command, null);
        formatted.Should().Be("oPON");
    }

    [Fact]
    public void MassHunter_StartBatch_InterpolatesBatchPath()
    {
        var run = PackGeneratorTestHost.Run(PackGeneratorTestHost.LoadMassHunter());
        var assembly = PackGeneratorTestHost.Emit(run);
        var type = assembly.GetType("Icpms.MassHunter.Protocol.StartBatchCommand");
        type.Should().NotBeNull();

        var command = Activator.CreateInstance(type!, "a.b");
        var formatted = type!.GetMethod("FormatWireMessage")!.Invoke(command, null);
        formatted.Should().Be("oBATCH.a.b");
    }

    [Fact]
    public void MassHunter_IgnitePlasmaReply_HasBusyFaultUnmatched()
    {
        var run = PackGeneratorTestHost.Run(PackGeneratorTestHost.LoadMassHunter());
        var assembly = PackGeneratorTestHost.Emit(run);
        var type = assembly.GetType("Icpms.MassHunter.Protocol.IgnitePlasmaReply");
        type.Should().NotBeNull();
        type!.IsEnum.Should().BeTrue();

        var names = Enum.GetNames(type);
        names.Should().Contain(new[] { "Ok", "Busy", "Fault", "Unmatched" });
    }

    [Fact]
    public void MassHunter_GeneratedCommand_ImplementsDeviceWireCommand()
    {
        var run = PackGeneratorTestHost.Run(PackGeneratorTestHost.LoadMassHunter());
        var sources = PackGeneratorTestHost.GeneratedSources(run).ToList();
        sources.Should().Contain(s => s.Contains("IgnitePlasmaCommand") &&
                                      s.Contains("IDeviceWireCommand") &&
                                      s.Contains("return \"oPON\""));
    }

    [Fact]
    public void BrokenYaml_ReportsDiagnostic_AndDoesNotCrash()
    {
        var files = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(
                @"C:\packs\broken\protocol.kable.yaml",
                "{{{{ this is not: [ valid yaml"));

        var run = PackGeneratorTestHost.Run(files);
        run.Diagnostics.Should().Contain(d => d.Severity == DiagnosticSeverity.Error);
        run.Diagnostics.Should().Contain(d => d.GetMessage().IndexOf("protocol.kable.yaml", StringComparison.OrdinalIgnoreCase) >= 0);
        PackGeneratorTestHost.GeneratedSources(run).Should().NotContain(s => s.Contains("IgnitePlasmaCommand"));
    }

    [Fact]
    public void InvalidPackVersion_ReportsValidationDiagnostic()
    {
        var files = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(
                @"C:\packs\badver\protocol.kable.yaml",
                "protocolPackVersion: 2\nname: BadVer\ncodec: AsciiLine\n"));

        var run = PackGeneratorTestHost.Run(files);
        run.Diagnostics.Should().Contain(d => d.Severity == DiagnosticSeverity.Error);
        run.Diagnostics.Should().Contain(d => d.GetMessage().IndexOf("version", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void MissingCsharpNamespace_ReportsInfo_AndUsesDefaultNamespace()
    {
        var files = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(
                @"C:\packs\Noname-Pack\protocol.kable.yaml",
                "protocolPackVersion: 1\nname: Noname-Pack\ncodec: AsciiLine\n"),
            new InMemoryAdditionalText(
                @"C:\packs\Noname-Pack\commands\ping.kable.yaml",
                "name: Ping\nmatch: PING\nok:\n  replies:\n    - ACK\n"));

        var run = PackGeneratorTestHost.Run(files);
        run.Diagnostics.Should().Contain(d => d.Severity == DiagnosticSeverity.Info);
        PackGeneratorTestHost.GeneratedSources(run)
            .Should()
            .Contain(s => s.Contains("namespace Kable.Generated.NonamePack"));
    }
}
