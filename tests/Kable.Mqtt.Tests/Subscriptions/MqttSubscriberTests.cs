namespace Kable.Mqtt.Tests.Subscriptions;

using Kable.Mqtt.Subscriptions;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Xunit;

public sealed class MqttSubscriberTests
{
    [Fact]
    public async Task FilterQosReconnectAndUnsubscribeUseRealBroker()
    {
        var factory = new MqttFactory();
        var (broker, port) = await MqttTelemetryTests.StartBrokerOnDynamicPortAsync(factory);
        using var brokerScope = broker;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            var options = new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).WithClientId("kable-sub").WithCleanSession().Build();
            await using var subscriber = new MqttSubscriber(options, new[]
            {
                new MqttTopicFilterBuilder().WithTopic("device/+/state").WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build()
            }, capacity: 2, retryDelay: TimeSpan.FromMilliseconds(50));
            await subscriber.StartAsync(timeout.Token);

            using var publisher = factory.CreateMqttClient();
            var pubOptions = new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).Build();
            await publisher.ConnectAsync(pubOptions, timeout.Token);

            await Publish("ignore/topic", 1);
            await Publish("device/a/state", 42);
            var message = await subscriber.Messages.ReadAsync(timeout.Token);
            Assert.Equal("device/a/state", message.Topic);
            Assert.Equal(new byte[] { 42 }, message.Payload.ToArray());
            Assert.Equal(MqttQualityOfServiceLevel.AtLeastOnce, message.Qos);

            await broker.StopAsync();
            await WaitUntil(() => !subscriber.IsConnected);
            await broker.StartAsync();
            await WaitUntil(() => subscriber.IsConnected);
            await publisher.ConnectAsync(pubOptions, timeout.Token);
            await Publish("device/b/state", 99);
            Assert.Equal(99, (await subscriber.Messages.ReadAsync(timeout.Token)).Payload.Span[0]);
            await subscriber.UnsubscribeAsync("device/+/state", timeout.Token);
            await Publish("device/a/state", 88);
            await subscriber.DisposeAsync();
            Assert.False(subscriber.Messages.TryRead(out _));
            Assert.True(subscriber.Messages.Completion.IsCompleted);

            async Task Publish(string topic, byte value) => await publisher.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic).WithPayload(new byte[] { value }).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), timeout.Token);
            async Task WaitUntil(Func<bool> condition)
            {
                while (!condition()) await Task.Delay(20, timeout.Token);
            }
        }
        finally
        {
            if (broker.IsStarted) await broker.StopAsync();
        }
    }

    [Fact]
    public async Task SaturatedConsumerIsBoundedAndCountsDrops()
    {
        var factory = new MqttFactory();
        var (broker, port) = await MqttTelemetryTests.StartBrokerOnDynamicPortAsync(factory);
        using var brokerScope = broker;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            await using var subscriber = new MqttSubscriber(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).Build(),
                new[] { new MqttTopicFilterBuilder().WithTopic("load/#").Build() }, capacity: 2);
            await subscriber.StartAsync(timeout.Token);
            using var publisher = factory.CreateMqttClient();
            await publisher.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).Build(), timeout.Token);
            for (int i = 0; i < 20; i++) await publisher.PublishAsync(new MqttApplicationMessageBuilder().WithTopic("load/a")
                .WithPayload(new byte[] { (byte)i }).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), timeout.Token);
            while (subscriber.ReceivedMessages < 20) await Task.Delay(10, timeout.Token);
            Assert.Equal(18, subscriber.DroppedMessages);
            Assert.Equal(2, subscriber.Messages.Count);
            await subscriber.DisposeAsync();
        }
        finally
        {
            if (broker.IsStarted) await broker.StopAsync();
        }
    }
}
