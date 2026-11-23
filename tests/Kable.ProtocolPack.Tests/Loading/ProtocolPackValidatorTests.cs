using Kable.ProtocolPack.Loading;
using Kable.ProtocolPack.Tests.Support;
using Xunit;

namespace Kable.ProtocolPack.Tests.Loading;

public sealed class ProtocolPackValidatorTests
{
    [Fact]
    public void Load_Rejects_ProtocolPackVersion2()
    {
        using var scope = PackTestFolder.CloneScoped("MassHunter-7700");
        PackTestFolder.ReplaceInFile(
            scope.Folder,
            "protocol.kable.yaml",
            "protocolPackVersion: 1",
            "protocolPackVersion: 2");

        var ex = Assert.Throws<ProtocolPackException>(() => ProtocolPackLoader.Load(scope.Folder));
        Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_Rejects_CodecOnCommandFile()
    {
        using var scope = PackTestFolder.CloneScoped("MassHunter-7700");
        PackTestFolder.ReplaceInFile(
            scope.Folder,
            Path.Combine("commands", "oPON.kable.yaml"),
            "name: IgnitePlasma",
            "name: IgnitePlasma\ncodec: AsciiLine");

        var ex = Assert.Throws<ProtocolPackException>(() => ProtocolPackLoader.Load(scope.Folder));
        Assert.Contains("codec", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("command", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_Rejects_DuplicateMatch()
    {
        using var scope = PackTestFolder.CloneScoped("MassHunter-7700");
        PackTestFolder.WriteAllText(
            scope.Folder,
            Path.Combine("commands", "dup.kable.yaml"),
            "name: DuplicateIgnite\nmatch: oPON\nok:\n  replies:\n    - ACK\n");

        var ex = Assert.Throws<ProtocolPackException>(() => ProtocolPackLoader.Load(scope.Folder));
        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("oPON", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_Rejects_PlaceholderWithoutParams()
    {
        using var scope = PackTestFolder.CloneScoped("MassHunter-7700");
        PackTestFolder.WriteAllText(
            scope.Folder,
            Path.Combine("commands", "oBATCH.kable.yaml"),
            "name: StartBatch\nmatch: \"oBATCH.{BatchPath}\"\nok:\n  replies:\n    - ACK\n");

        var ex = Assert.Throws<ProtocolPackException>(() => ProtocolPackLoader.Load(scope.Folder));
        Assert.Contains("BatchPath", ex.Message, StringComparison.Ordinal);
        Assert.Contains("params", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_Rejects_ModbusScalarValue()
    {
        using var scope = PackTestFolder.CloneScoped("ABB-TALYS");
        PackTestFolder.WriteAllText(
            scope.Folder,
            Path.Combine("maps", "scalar.kable.yaml"),
            "name: ScalarBlock\ntable: holding\naddr: 10\nvalue: 1\n");

        var ex = Assert.Throws<ProtocolPackException>(() => ProtocolPackLoader.Load(scope.Folder));
        Assert.Contains("value", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scalar", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_Rejects_InvalidMelsecCode()
    {
        using var scope = PackTestFolder.CloneScoped("Line-PLC");
        PackTestFolder.ReplaceInFile(
            scope.Folder,
            Path.Combine("maps", "D.kable.yaml"),
            "code: D",
            "code: QQ");

        var ex = Assert.Throws<ProtocolPackException>(() => ProtocolPackLoader.Load(scope.Folder));
        Assert.Contains("QQ", ex.Message, StringComparison.Ordinal);
        Assert.Contains("MelsecDeviceCode", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ValidMassHunter_ReturnsNoErrors()
    {
        var pack = ProtocolPackLoader.Load(PackTestFolder.MassHunter);
        Assert.Empty(ProtocolPackValidator.Validate(pack));
    }
}
