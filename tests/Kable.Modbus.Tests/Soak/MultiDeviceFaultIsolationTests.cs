namespace Kable.Modbus.Tests.Soak;

using Kable.Exceptions;
using Kable.Modbus.Devices;
using Kable.Modbus.Tests.Rtu;
using Xunit;

public sealed class MultiDeviceFaultIsolationTests
{
    [Fact]
    public async Task OneDeviceDisconnectDoesNotFailOtherDevices()
    {
        await using var broken = await RtuTestLink.OpenAsync();
        await using var healthy = await RtuTestLink.OpenAsync();
        await using var registry = new ModbusDeviceRegistry(new[]
        {
            new ModbusDeviceRegistration("broken", () => broken.Master),
            new ModbusDeviceRegistration("healthy", () => healthy.Master)
        });
        var failed = registry.Get("broken").ReadHoldingRegistersAsync(0, 1);
        var valid = registry.Get("healthy").ReadHoldingRegistersAsync(0, 1);
        await Task.WhenAll(broken.ReadRequestAsync(), healthy.ReadRequestAsync());
        await broken.DisconnectAsync();
        await healthy.RespondAsync([1, 3, 2, 0, 42]);
        await Assert.ThrowsAsync<DeviceDisconnectedException>(() => failed.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(new ushort[] { 42 }, await valid);
        var next = registry.Get("healthy").ReadHoldingRegistersAsync(1, 1);
        await healthy.ReadRequestAsync(); await healthy.RespondAsync([1, 3, 2, 0, 43]);
        Assert.Equal(new ushort[] { 43 }, await next);
        Assert.True(healthy.Master.IsConnected);
    }
}
