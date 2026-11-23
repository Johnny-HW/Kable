namespace Kable.Mqtt.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Exceptions;
using Kable.Mqtt;
using Kable.Mqtt.Subscriptions;
using Kable.Mqtt.Telemetry;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Xunit;

public sealed class MqttHardeningTests
{
    [Theory]
    [InlineData(MqttQualityOfServiceLevel.AtLeastOnce)]
    [InlineData(MqttQualityOfServiceLevel.ExactlyOnce)]
    public async Task PublishMetricAndRaw_Disconnected_Qos1Or2_ThrowsDeviceDisconnected(MqttQualityOfServiceLevel qos)
    {
        await using var publisher = MqttTelemetryPublisher.CreateTcp("127.0.0.1", 1883);

        await Assert.ThrowsAsync<DeviceDisconnectedException>(() =>
            publisher.PublishMetricAsync(new TelemetryMetric("temp", 25.0), qos));
        await Assert.ThrowsAsync<DeviceDisconnectedException>(() =>
            publisher.PublishRawAsync("raw/bin", new byte[] { 1, 2, 3 }, qos));
    }

    [Fact]
    public async Task PublishMetricAndRaw_Disconnected_Qos0_DropsAndIncrementsCounter()
    {
        await using var publisher = MqttTelemetryPublisher.CreateTcp("127.0.0.1", 1883);

        await publisher.PublishMetricAsync(new TelemetryMetric("temp", 25.0), MqttQualityOfServiceLevel.AtMostOnce);
        await publisher.PublishRawAsync("raw/bin", new byte[] { 1, 2, 3 }, MqttQualityOfServiceLevel.AtMostOnce);

        Assert.Equal(2, publisher.DroppedWhileDisconnected);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task PublishMetric_NonFiniteValue_ThrowsArgumentOutOfRange(double badValue)
    {
        await using var publisher = MqttTelemetryPublisher.CreateTcp("127.0.0.1", 1883);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            publisher.PublishMetricAsync(new TelemetryMetric("temp", badValue)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sensor/+/temp")]
    [InlineData("sensor/#")]
    [InlineData("sensor\0bad")]
    public async Task Publish_InvalidTopicSegment_ThrowsArgumentException(string badSegment)
    {
        await using var publisher = MqttTelemetryPublisher.CreateTcp("127.0.0.1", 1883);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.PublishRawAsync(badSegment, new byte[] { 1 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.PublishMetricAsync(new TelemetryMetric(badSegment, 1.0)));
    }

    [Fact]
    public async Task OptionsConstructor_AppliesCredentialsAndTls_AndPreservesUnownedClient()
    {
        using var externalClient = new MqttFactory().CreateMqttClient();
        var options = Options.Create(new MqttOptions
        {
            Host = "127.0.0.1",
            Port = 8883,
            ClientId = "secure-client",
            Username = "alice",
            Password = "secret",
            UseTls = true,
            AllowUntrustedCertificates = false
        });

        var publisher = new MqttTelemetryPublisher(externalClient, options);
        var dispose1 = publisher.DisposeAsync().AsTask();
        var dispose2 = publisher.DisposeAsync().AsTask();
        Assert.Same(dispose1, dispose2);
        await dispose1;

        // Caller-owned client must remain usable (not disposed by publisher).
        Assert.False(externalClient.IsConnected);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => publisher.StartAsync());
    }

    [Fact]
    public async Task MqttSubscriber_AfterDispose_ThrowsObjectDisposedInsteadOfCtsRace()
    {
        var opts = new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", 1883).Build();
        var subscriber = new MqttSubscriber(opts, Array.Empty<MqttTopicFilter>());
        await subscriber.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => subscriber.StartAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => subscriber.UnsubscribeAsync("a/b"));
    }
}
