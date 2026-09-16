namespace Microsoft.Extensions.DependencyInjection;

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kable.OpcUa;

public static class OpcUaServiceCollectionExtensions
{
    public static IServiceCollection AddKableOpcUa(
        this IServiceCollection services,
        Action<OpcUaOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<IOpcUaClientBridge>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpcUaOptions>>();
            var logger = sp.GetService<ILogger<OpcUaClientBridge>>();

            return new OpcUaClientBridge(options, logger);
        });

        return services;
    }
}
