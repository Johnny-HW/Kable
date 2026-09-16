namespace Kable.Mqtt.Tests;

using System;
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
    [Fact]
    public async Task MqttTelemetryPublisher_PublishMetric_BrokerReceivesValidJson()
    {
        // 1. 임베디드 인메모리 MQTT 서버 기동 (동적 포트 할당 또는 루프백 18883)
        int testPort = 18883;
        var mqttFactory = new MqttFactory();
        var serverOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(testPort)
            .Build();

        using var mqttServer = mqttFactory.CreateMqttServer(serverOptions);
        await mqttServer.StartAsync();

        var receivedTcs = new TaskCompletionSource<string>();

        // 2. 수신용 테스트 클라이언트 (Subscriber)
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

        // 3. Kable MqttTelemetryPublisher 발행 (Publisher)
        var publisher = MqttTelemetryPublisher.CreateTcp(
            host: "127.0.0.1",
            port: testPort,
            clientId: "KableTestPublisher",
            topicPrefix: "kable/telemetry");

        await publisher.StartAsync();

        var metric = new TelemetryMetric("pump_pressure", 4.25);
        await publisher.PublishMetricAsync(metric);

        // 4. 수신 검증
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        timeoutCts.Token.Register(() => receivedTcs.TrySetCanceled());

        string receivedJson = await receivedTcs.Task;
        Assert.NotNull(receivedJson);

        using var doc = JsonDocument.Parse(receivedJson);
        Assert.Equal("pump_pressure", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(4.25, doc.RootElement.GetProperty("value").GetDouble());

        // 5. 정리
        await publisher.DisposeAsync();
        await subscriberClient.DisconnectAsync();
        await mqttServer.StopAsync();
    }
}
