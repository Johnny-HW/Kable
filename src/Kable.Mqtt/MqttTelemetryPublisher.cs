namespace Kable.Mqtt;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kable.Mqtt.Telemetry;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

/// <summary>
/// MQTTnet 기반 초고속 설비 텔레메트리 발행기.
/// </summary>
public sealed class MqttTelemetryPublisher : IMqttTelemetryPublisher
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;
    private readonly string _topicPrefix;
    private readonly ILogger<MqttTelemetryPublisher>? _logger;
    private int _isDisposed;

    public bool IsConnected => _client.IsConnected;

    public MqttTelemetryPublisher(
        IMqttClient client,
        IOptions<MqttOptions> options,
        ILogger<MqttTelemetryPublisher>? logger = null)
        : this(
            client,
            new MqttClientOptionsBuilder()
                .WithTcpServer((options ?? throw new ArgumentNullException(nameof(options))).Value.Host, options.Value.Port)
                .WithClientId(options.Value.ClientId)
                .WithCleanSession(true)
                .Build(),
            options.Value.TopicPrefix,
            logger)
    {
    }

    public MqttTelemetryPublisher(
        IMqttClient client,
        MqttClientOptions options,
        string topicPrefix = "kable/telemetry",
        ILogger<MqttTelemetryPublisher>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _topicPrefix = (topicPrefix ?? throw new ArgumentNullException(nameof(topicPrefix))).TrimEnd('/');
        _logger = logger;
    }

    public static MqttTelemetryPublisher CreateTcp(
        string host,
        int port = 1883,
        string clientId = "KablePublisher",
        string topicPrefix = "kable/telemetry",
        ILogger<MqttTelemetryPublisher>? logger = null)
    {
        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(clientId)
            .WithCleanSession(true)
            .Build();

        return new MqttTelemetryPublisher(client, options, topicPrefix, logger);
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
    /// Utf8JsonWriter와 재사용 버퍼를 활용하여 불필요한 객체 할당을 최소화합니다.
    /// </summary>
    public async Task PublishMetricAsync(TelemetryMetric metric, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default)
    {
        if (!_client.IsConnected) return;

        string topic = $"{_topicPrefix}/{metric.Name}";
        
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("name", metric.Name);
            writer.WriteNumber("value", metric.Value);
            writer.WriteNumber("timestamp", metric.TimestampTicks);

            if (metric.Tags != null)
            {
                writer.WriteStartObject("tags");
                foreach (var (k, v) in metric.Tags)
                {
                    writer.WriteString(k, v);
                }
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNull("tags");
            }
            writer.WriteEndObject();
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(stream.ToArray())
            .WithQualityOfServiceLevel(qos)
            .Build();

        await _client.PublishAsync(message, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 원시(Raw) 바이트 페이로드를 특정 하위 토픽으로 발행합니다.
    /// underlying byte[] 배열이 있는 경우 불필요한 복사(ToArray) 없이 직접 ArraySegment로 전달합니다.
    /// </summary>
    public async Task PublishRawAsync(string subTopic, ReadOnlyMemory<byte> payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default)
    {
        if (!_client.IsConnected) return;

        string topic = $"{_topicPrefix}/{subTopic.TrimStart('/')}";

        var builder = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithQualityOfServiceLevel(qos);

        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(payload, out ArraySegment<byte> segment))
        {
            builder.WithPayload(segment);
        }
        else
        {
            builder.WithPayload(payload.ToArray());
        }

        var message = builder.Build();
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
