namespace Kable.Mqtt.Tests;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kable.Mqtt;
using Kable.Mqtt.Telemetry;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
using Xunit;

public class MqttTelemetryTests
{
    internal static async Task<(MqttServer Server, int Port)> StartBrokerOnDynamicPortAsync(MqttFactory factory)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            int port;
            using (var probe = new TcpListener(IPAddress.Loopback, 0))
            {
                probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
            }

            var options = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
                .WithDefaultEndpointPort(port)
                .Build();
            var server = factory.CreateMqttServer(options);
            try
            {
                await server.StartAsync();
                return (server, port);
            }
            catch (SocketException) when (attempt < 4)
            {
                server.Dispose();
            }
        }

        throw new InvalidOperationException("Could not allocate a free loopback port for test MQTT broker.");
    }

    [Fact]
    public async Task MqttTelemetryPublisher_PublishMetric_BrokerReceivesValidJson()
    {
        var mqttFactory = new MqttFactory();
        var (mqttServer, testPort) = await StartBrokerOnDynamicPortAsync(mqttFactory);
        using var serverScope = mqttServer;

        try
        {
            var receivedTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var subscriberClient = mqttFactory.CreateMqttClient();
            var subOptions = new MqttClientOptionsBuilder()
                .WithTcpServer("127.0.0.1", testPort)
                .WithClientId("TestSubscriber")
                .Build();

            subscriberClient.ApplicationMessageReceivedAsync += e =>
            {
                string payloadString = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                receivedTcs.TrySetResult(payloadString);
                return Task.CompletedTask;
            };

            await subscriberClient.ConnectAsync(subOptions);
            await subscriberClient.SubscribeAsync("kable/telemetry/#");

            await using var publisher = MqttTelemetryPublisher.CreateTcp(
                host: "127.0.0.1",
                port: testPort,
                clientId: "KableTestPublisher",
                topicPrefix: "kable/telemetry");

            try
            {
                await publisher.StartAsync();

                var metric = new TelemetryMetric("pump_pressure", 4.25);
                await publisher.PublishMetricAsync(metric);

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var registration = timeoutCts.Token.Register(() => receivedTcs.TrySetCanceled(timeoutCts.Token));

                string receivedJson = await receivedTcs.Task;
                Assert.NotNull(receivedJson);

                using var doc = JsonDocument.Parse(receivedJson);
                Assert.Equal("pump_pressure", doc.RootElement.GetProperty("name").GetString());
                Assert.Equal(4.25, doc.RootElement.GetProperty("value").GetDouble());
            }
            finally
            {
                if (subscriberClient.IsConnected)
                {
                    await subscriberClient.DisconnectAsync();
                }
            }
        }
        finally
        {
            await mqttServer.StopAsync();
        }
    }
}
