namespace Microsoft.Extensions.DependencyInjection;

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kable.Engine;
using Kable.Extensions;
using Kable.Modbus;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Kable.Modbus.Rtu;
using Kable.Transports;

public static class ModbusServiceCollectionExtensions
{
    /// <summary>Registers one RTU bus. Resolve ModbusRtuMaster when TCP and RTU coexist.</summary>
    public static IServiceCollection AddKableModbusRtu(this IServiceCollection services, Action<ModbusRtuOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<ModbusRtuMaster>(sp => new ModbusRtuMaster(sp.GetRequiredService<IOptions<ModbusRtuOptions>>().Value));
        services.AddSingleton<IModbusCoilMaster>(sp => sp.GetRequiredService<ModbusRtuMaster>());
        services.AddSingleton<IModbusMaster>(sp => sp.GetRequiredService<ModbusRtuMaster>());
        return services;
    }

    public static IServiceCollection AddKableModbusTcp(
        this IServiceCollection services,
        Action<ModbusTcpOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<IModbusCoilMaster>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ModbusTcpOptions>>().Value;
            var logger = sp.GetService<ILogger<ModbusTcpMaster>>();

            var factory = new TcpConnectionFactory(options.Host, options.Port);
            var codec = new ModbusTcpCodec();
            var session = new KableSession<ModbusTcpMessage>(factory, codec);

            return new ModbusTcpMaster(session, sp.GetRequiredService<IOptions<ModbusTcpOptions>>(), logger);
        });
        services.AddSingleton<IModbusMaster>(sp => sp.GetRequiredService<IModbusCoilMaster>());

        return services;
    }
}
