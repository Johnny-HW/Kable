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
            var options = sp.GetRequiredService<IOptions<MqttOptions>>().Value;
            var logger = sp.GetService<ILogger<MqttTelemetryPublisher>>();

            var factory = new MqttFactory();
            var client = factory.CreateMqttClient();

            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(options.Host, options.Port)
                .WithClientId(options.ClientId)
                .WithCleanSession(true)
                .Build();

            return new MqttTelemetryPublisher(client, clientOptions, options.TopicPrefix, logger);
        });

        return services;
    }
}
