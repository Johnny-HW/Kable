namespace Kable.Modbus.Tests.Devices;

using Kable.Modbus.Devices;
using Kable.Modbus.Tests.Coils;
using Xunit;

public sealed class ModbusDeviceRegistryTests
{
    [Fact]
    public async Task NamesAreIndependentAndFactoriesAreLazy()
    {
        int created = 0;
        await using var registry = new ModbusDeviceRegistry(new[]
        {
            new ModbusDeviceRegistration("plc", () => { created++; return Master(10); }),
            new ModbusDeviceRegistration("sensor", () => { created++; return Master(20); })
        });
        Assert.Equal(0, created);
        Assert.Same(registry.Get("plc"), registry.Get("plc"));
        Assert.NotSame(registry.Get("plc"), registry.Get("sensor"));
        Assert.Equal(2, created);
        Assert.Equal(new ushort[] { 10 }, await registry.Get("plc").ReadHoldingRegistersAsync(0, 1));
        Assert.Equal(new ushort[] { 20 }, await registry.Get("sensor").ReadHoldingRegistersAsync(0, 1));
        Assert.Throws<KeyNotFoundException>(() => registry.Get("missing"));
        await registry.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => registry.Get("plc"));
    }

    [Fact]
    public void DuplicateNamesFailBeforeCreatingConnections()
    {
        Assert.Throws<ArgumentException>(() => new ModbusDeviceRegistry(new[]
        {
            new ModbusDeviceRegistration("same", () => Master(1)),
            new ModbusDeviceRegistration("same", () => Master(2))
        }));
    }

    private static ModbusTcpMaster Master(byte value) => new(new ReplySession(r =>
        new(r.TransactionId, r.UnitId, r.FunctionCode, new byte[] { 2, 0, value })), new ModbusTcpOptions());
}
