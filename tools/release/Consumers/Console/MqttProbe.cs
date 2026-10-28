using System.Net;
using System.Net.Sockets;
using Kable.Mqtt;
using Kable.Mqtt.Subscriptions;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;

internal static class MqttProbe
{
    internal static async Task RunAsync(CancellationToken ct)
    {
        using var picker = new TcpListener(IPAddress.Loopback, 0);
        picker.Start(); int port = ((IPEndPoint)picker.LocalEndpoint).Port; picker.Stop();
        using var broker = new MqttFactory().CreateMqttServer(new MqttServerOptionsBuilder().WithDefaultEndpoint().WithDefaultEndpointPort(port).Build());
        await broker.StartAsync();
        try
        {
            await using var subscriber = new MqttSubscriber(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).Build(),
                new[] { new MqttTopicFilterBuilder().WithTopic("probe/#").Build() });
            await subscriber.StartAsync(ct);
            await using var publisher = MqttTelemetryPublisher.CreateTcp("127.0.0.1", port, topicPrefix: "probe");
            await publisher.StartAsync(ct);
            await publisher.PublishRawAsync("state", new byte[] { 42 }, ct: ct);
            var value = await subscriber.Messages.ReadAsync(ct);
            if (value.Topic != "probe/state" || value.Payload.Span[0] != 42) throw new Exception("Packaged MQTT subscription failed.");
        }
        finally { await broker.StopAsync(); }
    }
}
