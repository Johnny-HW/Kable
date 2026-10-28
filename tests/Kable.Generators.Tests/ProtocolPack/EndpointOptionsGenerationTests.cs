namespace Kable.Generators.Tests.ProtocolPack;

using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

public class EndpointOptionsGenerationTests
{
    [Fact]
    public void MassHunter_CreateDeviceOptions_UsesEndpointPort9000()
    {
        var run = PackGeneratorTestHost.Run(PackGeneratorTestHost.LoadMassHunter());
        run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var assembly = PackGeneratorTestHost.Emit(run);
        var type = assembly.GetType("Icpms.MassHunter.Protocol.MassHunter7700Protocol");
        type.Should().NotBeNull();

        var options = type!.GetMethod("CreateDeviceOptions")!.Invoke(null, null);
        options.Should().NotBeNull();
        options!.GetType().GetProperty("Port")!.GetValue(options).Should().Be(9000);
        options.GetType().GetProperty("Host")!.GetValue(options).Should().Be("127.0.0.1");
        options.GetType().GetProperty("Transport")!.GetValue(options).Should().Be("Tcp");
    }

    [Fact]
    public void MassHunter_ProtocolClass_ExposesAsciiLineCodecConstant()
    {
        var run = PackGeneratorTestHost.Run(PackGeneratorTestHost.LoadMassHunter());
        PackGeneratorTestHost.GeneratedSources(run)
            .Should()
            .Contain(s => s.Contains("class MassHunter7700Protocol") &&
                          s.Contains("public const string Codec = \"AsciiLine\""));
    }

    [Fact]
    public void SerialEndpoint_SeedsSerialFields()
    {
        var files = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(
                @"C:\packs\SerialPack\protocol.kable.yaml",
                @"protocolPackVersion: 1
name: SerialPack
codec: AsciiLine
endpoint:
  transport: Serial
  role: Server
  portName: COM4
  baudRate: 115200
  parity: Even
  dataBits: 7
  stopBits: Two
csharp:
  namespace: Serial.Protocol
"));

        var run = PackGeneratorTestHost.Run(files);
        var assembly = PackGeneratorTestHost.Emit(run);
        var type = assembly.GetType("Serial.Protocol.SerialPackProtocol");
        var options = type!.GetMethod("CreateDeviceOptions")!.Invoke(null, null);
        options!.GetType().GetProperty("Transport")!.GetValue(options).Should().Be("Serial");
        options.GetType().GetProperty("PortName")!.GetValue(options).Should().Be("COM4");
        options.GetType().GetProperty("BaudRate")!.GetValue(options).Should().Be(115200);
        options.GetType().GetProperty("Parity")!.GetValue(options).Should().Be("Even");
        options.GetType().GetProperty("DataBits")!.GetValue(options).Should().Be(7);
        options.GetType().GetProperty("StopBits")!.GetValue(options).Should().Be("Two");
    }

    [Fact]
    public void NamedPipeEndpoint_SeedsPipeFields()
    {
        var files = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(
                @"C:\packs\PipePack\protocol.kable.yaml",
                @"protocolPackVersion: 1
name: PipePack
codec: AsciiLine
endpoint:
  transport: NamedPipe
  role: Client
  pipeName: kable_pipe
  serverName: .
csharp:
  namespace: Pipe.Protocol
"));

        var run = PackGeneratorTestHost.Run(files);
        var assembly = PackGeneratorTestHost.Emit(run);
        var type = assembly.GetType("Pipe.Protocol.PipePackProtocol");
        var options = type!.GetMethod("CreateDeviceOptions")!.Invoke(null, null);
        options!.GetType().GetProperty("Transport")!.GetValue(options).Should().Be("NamedPipe");
        options.GetType().GetProperty("PipeName")!.GetValue(options).Should().Be("kable_pipe");
        options.GetType().GetProperty("ServerName")!.GetValue(options).Should().Be(".");
    }
}
