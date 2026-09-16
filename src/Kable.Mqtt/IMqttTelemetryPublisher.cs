namespace Kable.Mqtt;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Mqtt.Telemetry;
using MQTTnet.Protocol;

public interface IMqttTelemetryPublisher : IAsyncDisposable
{
    bool IsConnected { get; }
    Task StartAsync(CancellationToken ct = default);
    Task PublishMetricAsync(TelemetryMetric metric, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default);
    Task PublishRawAsync(string subTopic, ReadOnlyMemory<byte> payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default);
}
