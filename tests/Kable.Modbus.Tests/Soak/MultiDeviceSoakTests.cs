namespace Kable.Modbus.Tests.Soak;

using System.Diagnostics;
using Kable.Modbus.Devices;
using Kable.Modbus.Rtu;
using Kable.Modbus.Tests.Rtu;
using Xunit;
using Xunit.Abstractions;

[CollectionDefinition("ModbusSoak", DisableParallelization = true)]
public sealed class ModbusSoakCollection;

[Collection("ModbusSoak")]
public sealed class MultiDeviceSoakTests(ITestOutputHelper output)
{
    [Fact]
    public async Task MixedTcpRtuDevices_RemainIsolatedUnderContinuousLoad()
    {
        int seconds = int.TryParse(Environment.GetEnvironmentVariable("KABLE_SOAK_SECONDS"), out int requested) ? Math.Clamp(requested, 1, 3600) : 3;
        await using var tcp1 = new LoopbackModbusDevice(10);
        await using var tcp2 = new LoopbackModbusDevice(20);
        await using var rtu1 = await RtuTestLink.OpenAsync(new ModbusRtuOptions { BaudRate = 115200 });
        await using var rtu2 = await RtuTestLink.OpenAsync(new ModbusRtuOptions { BaudRate = 115200 });
        await using var registry = new ModbusDeviceRegistry(new[]
        {
            new ModbusDeviceRegistration("tcp1", () => tcp1.Master), new ModbusDeviceRegistration("tcp2", () => tcp2.Master),
            new ModbusDeviceRegistration("rtu1", () => rtu1.Master), new ModbusDeviceRegistration("rtu2", () => rtu2.Master)
        });
        await registry.ConnectAsync("tcp1"); await registry.ConnectAsync("tcp2");
        var watch = Stopwatch.StartNew();
        long baseline = GC.GetTotalMemory(true);
        long[] counts = new long[4];
        await Task.WhenAll(TcpLoad("tcp1", 10, 0), TcpLoad("tcp2", 20, 1), RtuLoad("rtu1", rtu1, 30, 2), RtuLoad("rtu2", rtu2, 40, 3));
        long retained = GC.GetTotalMemory(true) - baseline;
        output.WriteLine($"Duration={watch.Elapsed.TotalSeconds:F2}s; completed={string.Join(',', counts)}; managed retained delta={retained} bytes");
        Assert.All(counts, count => Assert.True(count > 10));
        Assert.True(retained < 16 * 1024 * 1024, $"Unexpected retained memory growth: {retained}");
        await registry.DisposeAsync();
        Assert.False(rtu1.Master.IsConnected); Assert.False(rtu2.Master.IsConnected);

        async Task TcpLoad(string name, ushort expected, int index)
        {
            while (watch.Elapsed.TotalSeconds < seconds)
            {
                Assert.Equal(new[] { expected }, await registry.Get(name).ReadHoldingRegistersAsync(0, 1));
                counts[index]++;
            }
        }
        async Task RtuLoad(string name, RtuTestLink link, byte expected, int index)
        {
            while (watch.Elapsed.TotalSeconds < seconds)
            {
                var pending = registry.Get(name).ReadHoldingRegistersAsync(0, 1);
                await link.ReadRequestAsync();
                if (counts[index] % 31 == 0) await Task.Delay(2);
                await link.RespondAsync([1, 3, 2, 0, expected]);
                Assert.Equal(new ushort[] { expected }, await pending);
                counts[index]++;
            }
        }
    }
}
