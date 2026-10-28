namespace Kable.Tests.Cases.Configuration;

using Kable.Modbus.Devices;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class NamedModbusRegistrationTests
{
    [Fact]
    public async Task MixedTcpAndRtuRegistrationsHaveIndependentSettings()
    {
        var services = new ServiceCollection();
        services.AddKableModbusTcpDevice("plc", options => { options.DefaultUnitId = 10; options.Timeout = TimeSpan.FromSeconds(1); });
        services.AddKableModbusRtuDevice("sensor", options => { options.DefaultUnitId = 20; options.Timeout = TimeSpan.FromSeconds(2); });
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ModbusDeviceRegistry>();
        Assert.Equal(10, registry.Get("plc").DefaultUnitId);
        Assert.Equal(20, registry.Get("sensor").DefaultUnitId);
        Assert.Equal(TimeSpan.FromSeconds(1), registry.Get("plc").DefaultTimeout);
        Assert.Equal(TimeSpan.FromSeconds(2), registry.Get("sensor").DefaultTimeout);
        registry.Get("plc").DefaultUnitId = 30;
        Assert.Equal(20, registry.Get("sensor").DefaultUnitId);
    }

    [Fact]
    public async Task DuplicateNamesFailDuringRegistryResolution()
    {
        var services = new ServiceCollection();
        services.AddKableModbusTcpDevice("duplicate", _ => { });
        services.AddKableModbusRtuDevice("duplicate", _ => { });
        await using var provider = services.BuildServiceProvider();
        Assert.Throws<ArgumentException>(() => provider.GetRequiredService<ModbusDeviceRegistry>());
    }
}
