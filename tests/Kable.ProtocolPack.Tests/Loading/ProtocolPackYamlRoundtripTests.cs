using System.Globalization;
using System.Linq;
using Kable.ProtocolPack.Loading;
using Kable.ProtocolPack.Models;
using Kable.ProtocolPack.Tests.Support;
using Xunit;

namespace Kable.ProtocolPack.Tests.Loading;

public sealed class ProtocolPackYamlRoundtripTests
{
    [Fact]
    public void Roundtrip_MassHunter_PreservesNameAndMatch()
    {
        var original = ProtocolPackLoader.Load(PackTestFolder.MassHunter);
        var reloaded = ReloadToTemp(original);

        Assert.Equal(original.Name, reloaded.Name);
        Assert.Equal(original.Codec, reloaded.Codec);
        Assert.Equal(
            original.Commands.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal),
            reloaded.Commands.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(
            original.Commands.Select(c => c.Match).OrderBy(m => m, StringComparer.Ordinal),
            reloaded.Commands.Select(c => c.Match).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void Roundtrip_AbbTalys_PreservesMapNamesAndValues()
    {
        var original = ProtocolPackLoader.Load(PackTestFolder.AbbTalys);
        var reloaded = ReloadToTemp(original);

        Assert.Equal(original.Name, reloaded.Name);
        AssertMapsEquivalent(original.Maps, reloaded.Maps);

        var status = reloaded.Maps.Single(m => m.Name == "StatusBlock");
        Assert.Equal(new[] { 1, 0, 42 }, status.Values.Select(ToInt32).ToArray());
    }

    [Fact]
    public void Roundtrip_LinePlc_PreservesMapNamesCodesAndValues()
    {
        var original = ProtocolPackLoader.Load(PackTestFolder.LinePlc);
        var reloaded = ReloadToTemp(original);

        Assert.Equal(original.Name, reloaded.Name);
        Assert.Equal(
            original.Maps.Select(m => m.Code).OrderBy(c => c, StringComparer.Ordinal),
            reloaded.Maps.Select(m => m.Code).OrderBy(c => c, StringComparer.Ordinal));
        AssertMapsEquivalent(original.Maps, reloaded.Maps);
    }

    private static ProtocolPackDocument ReloadToTemp(ProtocolPackDocument original)
    {
        var folder = Path.Combine(Path.GetTempPath(), "KableProtocolPackTests", Guid.NewGuid().ToString("N"));
        ProtocolPackLoader.Save(folder, original);
        return ProtocolPackLoader.Load(folder);
    }

    private static void AssertMapsEquivalent(IList<MapSpec> expected, IList<MapSpec> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        var actualByName = actual.ToDictionary(m => m.Name, StringComparer.Ordinal);
        foreach (var map in expected)
        {
            Assert.True(actualByName.ContainsKey(map.Name), "Missing map " + map.Name);
            var copy = actualByName[map.Name];
            Assert.Equal(map.Table, copy.Table);
            Assert.Equal(map.Code, copy.Code);
            Assert.Equal(map.Addr, copy.Addr);
            Assert.Equal(NormalizeValues(map.Values), NormalizeValues(copy.Values));
        }
    }

    private static int[] NormalizeValues(IList<object> values)
    {
        return values.Select(ToInt32).ToArray();
    }

    private static int ToInt32(object value)
    {
        if (value is bool flag)
        {
            return flag ? 1 : 0;
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
}
