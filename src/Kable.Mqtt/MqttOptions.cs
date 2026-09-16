namespace Kable.Mqtt;

using System;

public sealed class MqttOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = "KablePublisher";
    public string TopicPrefix { get; set; } = "kable/telemetry";
}
