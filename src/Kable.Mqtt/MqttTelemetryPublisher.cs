namespace Kable.Mqtt;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kable.Mqtt.Telemetry;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

/// <summary>
/// MQTTnet 기반 초고속 설비 텔레메트리 발행기.
/// </summary>
public sealed class MqttTelemetryPublisher : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;
    private readonly string _topicPrefix;
    private int _isDisposed;

    public bool IsConnected => _client.IsConnected;

    public MqttTelemetryPublisher(IMqttClient client, MqttClientOptions options, string topicPrefix = "kable/telemetry")
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _topicPrefix = topicPrefix.TrimEnd('/');
    }

    public static MqttTelemetryPublisher CreateTcp(string host, int port = 1883, string clientId = "KablePublisher", string topicPrefix = "kable/telemetry")
    {
        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(clientId)
            .WithCleanSession(true)
            .Build();

        return new MqttTelemetryPublisher(client, options, topicPrefix);
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (!_client.IsConnected)
        {
            await _client.ConnectAsync(_options, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 단일 텔레메트리 메트릭을 JSON 형태로 MQTT 브로커에 비동기 발행합니다.
    /// </summary>
    public async Task PublishMetricAsync(TelemetryMetric metric, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default)
    {
        if (!_client.IsConnected) return;

        string topic = $"{_topicPrefix}/{metric.Name}";
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            name = metric.Name,
            value = metric.Value,
            timestamp = metric.TimestampTicks,
            tags = metric.Tags
        });

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(qos)
            .Build();

        await _client.PublishAsync(message, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 원시(Raw) 바이트 페이로드를 특정 하위 토픽으로 발행합니다.
    /// </summary>
    public async Task PublishRawAsync(string subTopic, ReadOnlyMemory<byte> payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default)
    {
        if (!_client.IsConnected) return;

        string topic = $"{_topicPrefix}/{subTopic.TrimStart('/')}";
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload.ToArray())
            .WithQualityOfServiceLevel(qos)
            .Build();

        await _client.PublishAsync(message, ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

        if (_client.IsConnected)
        {
            try
            {
                await _client.DisconnectAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore disconnect error on shutdown
            }
        }

        _client.Dispose();
    }
}
