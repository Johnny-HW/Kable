namespace Microsoft.Extensions.DependencyInjection;

using Kable.Engine;
using Kable.Modbus;
using Kable.Modbus.Codecs;
using Kable.Modbus.Devices;
using Kable.Modbus.Messages;
using Kable.Modbus.Rtu;
using Kable.Transports;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ModbusDeviceServiceCollectionExtensions
{
    public static IServiceCollection AddKableModbusDevice(this IServiceCollection services, string name, Func<IModbusCoilMaster> create)
    {
        services.AddSingleton(new ModbusDeviceRegistration(name, create));
        services.TryAddSingleton<ModbusDeviceRegistry>();
        return services;
    }
    public static IServiceCollection AddKableModbusTcpDevice(this IServiceCollection services, string name, Action<ModbusTcpOptions> configure)
    {
        var options = new ModbusTcpOptions();
        configure(options);
        return services.AddKableModbusDevice(name, () => new ModbusTcpMaster(
            new KableSession<ModbusTcpMessage>(new TcpConnectionFactory(options.Host, options.Port), new ModbusTcpCodec()), options));
    }
    public static IServiceCollection AddKableModbusRtuDevice(this IServiceCollection services, string name, Action<ModbusRtuOptions> configure)
    {
        var options = new ModbusRtuOptions();
        configure(options);
        return services.AddKableModbusDevice(name, () => new ModbusRtuMaster(options));
    }
}
