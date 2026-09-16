namespace Microsoft.Extensions.DependencyInjection;

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kable.Mqtt;
using MQTTnet;
using MQTTnet.Client;

public static class MqttServiceCollectionExtensions
{
    public static IServiceCollection AddKableMqttPublisher(
        this IServiceCollection services,
        Action<MqttOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<IMqttTelemetryPublisher>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MqttOptions>>();
            var logger = sp.GetService<ILogger<MqttTelemetryPublisher>>();

            var factory = new MqttFactory();
            var client = factory.CreateMqttClient();

            return new MqttTelemetryPublisher(client, options, logger);
        });

        return services;
    }
}
