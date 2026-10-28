using System.Globalization;
using System.Linq;
using Kable.ProtocolPack.Loading;
using Kable.ProtocolPack.Tests.Support;
using Xunit;

namespace Kable.ProtocolPack.Tests.Loading;

public sealed class ProtocolPackLoaderTests
{
    [Fact]
    public void Load_MassHunter_HasAsciiLineAndThreeCommands()
    {
        var pack = ProtocolPackLoader.Load(PackTestFolder.MassHunter);

        Assert.Equal(1, pack.ProtocolPackVersion);
        Assert.Equal("MassHunter-7700", pack.Name);
        Assert.Equal("AsciiLine", pack.Codec);
        Assert.Equal("Tcp", pack.Endpoint?.Transport);
        Assert.Equal(9000, pack.Endpoint?.Port);
        Assert.Equal(3, pack.Commands.Count);
        Assert.Empty(pack.Maps);

        Assert.Contains(pack.Commands, c => c.Name == "IgnitePlasma" && c.Match == "oPON");
        Assert.Contains(pack.Commands, c => c.Name == "QueryStatus" && c.Match == "qSTAT");
        Assert.Contains(pack.Commands, c => c.Name == "Abort" && c.Match == "oABORT");

        var ignite = pack.Commands.Single(c => c.Name == "IgnitePlasma");
        Assert.Equal(new[] { "ACK" }, ignite.Ok?.Replies);
        Assert.Equal(2, ignite.Errors.Count);
        Assert.Contains(ignite.Errors, e => e.Name == "busy" && e.Replies.Contains("ERR:BUSY"));
        Assert.Equal(new[] { "NAK" }, pack.Unmatched?.Replies);
    }

    [Fact]
    public void Load_AbbTalys_HasModbusTcpAndThreeMapBlocks()
    {
        var pack = ProtocolPackLoader.Load(PackTestFolder.AbbTalys);

        Assert.Equal("ABB-TALYS", pack.Name);
        Assert.Equal("ModbusTcp", pack.Codec);
        Assert.Equal(1, pack.UnitId);
        Assert.Empty(pack.Commands);
        Assert.Equal(3, pack.Maps.Count);

        var status = pack.Maps.Single(m => m.Name == "StatusBlock");
        Assert.Equal("holding", status.Table);
        Assert.Equal(100, status.Addr);
        Assert.Equal(new[] { 1, 0, 42 }, status.Values.Select(ToInt32).ToArray());

        var spare = pack.Maps.Single(m => m.Name == "Spare");
        Assert.Equal(200, spare.Range?.Start);
        Assert.Equal(16, spare.Range?.Count);
        Assert.Equal(0, ToInt32(spare.Fill!));

        var coils = pack.Maps.Single(m => m.Name == "Digitals");
        Assert.Equal("coil", coils.Table);
        Assert.Equal(new[] { true, false, true }, coils.Values.Cast<bool>().ToArray());
    }

    [Fact]
    public void Load_LinePlc_HasMelsecAndTwoMaps()
    {
        var pack = ProtocolPackLoader.Load(PackTestFolder.LinePlc);

        Assert.Equal("Line-PLC", pack.Name);
        Assert.Equal("MelsecSlmp3E", pack.Codec);
        Assert.Empty(pack.Commands);
        Assert.Equal(2, pack.Maps.Count);

        var data = pack.Maps.Single(m => m.Name == "DataBlock");
        Assert.Equal("D", data.Code);
        Assert.Equal(1000, data.Addr);
        Assert.Equal(8, data.Points);
        Assert.Equal(new[] { 0, 0 }, data.Values.Select(ToInt32).ToArray());

        var inputs = pack.Maps.Single(m => m.Name == "Inputs");
        Assert.Equal("X", inputs.Code);
        Assert.Equal(0, inputs.Addr);
        Assert.Equal(16, inputs.Points);
    }

    [Fact]
    public void Load_MissingProtocolFile_Throws()
    {
        var folder = Path.Combine(Path.GetTempPath(), "KableProtocolPackTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var ex = Assert.Throws<ProtocolPackException>(() => ProtocolPackLoader.Load(folder));
        Assert.Contains("protocol.kable.yaml", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static int ToInt32(object value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);
}
