namespace Kable.Modbus.Tests.Devices;

using Kable.Modbus.Devices;
using Xunit;

public sealed class ModbusRegistryLifetimeTests
{
    [Fact]
    public async Task CleanupFailureStillDisposesOtherDevicesAndRejectsReentrancy()
    {
        ModbusDeviceRegistry? registry = null;
        int cleaned = 0;
        var first = new LifetimeMaster(() =>
        {
            Assert.Throws<ObjectDisposedException>(() => registry!.Get("second"));
            throw new IOException("injected cleanup failure");
        });
        var second = new LifetimeMaster(() => { cleaned++; return default; });
        registry = new ModbusDeviceRegistry(new[]
        {
            new ModbusDeviceRegistration("first", () => first), new ModbusDeviceRegistration("second", () => second)
        });
        registry.Get("first"); registry.Get("second");
        await Assert.ThrowsAsync<AggregateException>(() => registry.DisposeAsync().AsTask());
        Assert.Equal(1, cleaned);
    }

    private sealed class LifetimeMaster(Func<ValueTask> dispose) : IModbusCoilMaster
    {
        public byte DefaultUnitId { get; set; }
        public TimeSpan DefaultTimeout { get; set; }
        public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => dispose();
        public Task<bool[]> ReadCoilsAsync(ushort a, ushort c, byte? u = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool[]> ReadDiscreteInputsAsync(ushort a, ushort c, byte? u = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ushort[]> ReadInputRegistersAsync(ushort a, ushort c, byte? u = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ushort[]> ReadHoldingRegistersAsync(ushort a, ushort c, byte? u = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task WriteMultipleCoilsAsync(ushort a, bool[] v, byte? u = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task WriteSingleCoilAsync(ushort a, bool v, byte? u = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task WriteSingleRegisterAsync(ushort a, ushort v, byte? u = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task WriteMultipleRegistersAsync(ushort a, ushort[] v, byte? u = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
