namespace Microsoft.Extensions.DependencyInjection;

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kable.Engine;
using Kable.Extensions;
using Kable.Modbus;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Kable.Transports;

public static class ModbusServiceCollectionExtensions
{
    public static IServiceCollection AddKableModbusTcp(
        this IServiceCollection services,
        Action<ModbusTcpOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<IModbusMaster>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ModbusTcpOptions>>().Value;
            var logger = sp.GetService<ILogger<ModbusTcpMaster>>();

            var factory = new TcpConnectionFactory(options.Host, options.Port);
            var codec = new ModbusTcpCodec();
            var session = new KableSession<ModbusTcpMessage>(factory, codec);

            session.StartAsync().AsTask().GetAwaiter().GetResult();

            return new ModbusTcpMaster(session, sp.GetRequiredService<IOptions<ModbusTcpOptions>>(), logger);
        });

        return services;
    }
}
