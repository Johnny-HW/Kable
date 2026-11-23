namespace Kable.Mqtt;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kable.Exceptions;
using Kable.Mqtt.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly bool _ownsClient;
    private readonly object _lifetime = new();
    private Task? _disposal;
    private int _isDisposed;
    private long _droppedWhileDisconnected;

    public bool IsConnected => Volatile.Read(ref _isDisposed) == 0 && _client.IsConnected;

    /// <summary>QoS 0 발행 중 연결 끊김으로 드롭된 메시지 수.</summary>
    public long DroppedWhileDisconnected => Interlocked.Read(ref _droppedWhileDisconnected);

    public MqttTelemetryPublisher(
        IMqttClient client,
        IOptions<MqttOptions> options,
        ILogger<MqttTelemetryPublisher>? logger = null)
        : this(client, options, logger, ownsClient: false)
    {
    }

    public MqttTelemetryPublisher(
        IMqttClient client,
        IOptions<MqttOptions> options,
        ILogger<MqttTelemetryPublisher>? logger,
        bool ownsClient)
        : this(
            client,
            BuildClientOptions((options ?? throw new ArgumentNullException(nameof(options))).Value),
            options.Value.TopicPrefix,
            logger,
            ownsClient)
    {
    }

    public MqttTelemetryPublisher(
        IMqttClient client,
        MqttClientOptions options,
        string topicPrefix = "kable/telemetry",
        ILogger<MqttTelemetryPublisher>? logger = null)
        : this(client, options, topicPrefix, logger, ownsClient: false)
    {
    }

    public MqttTelemetryPublisher(
        IMqttClient client,
        MqttClientOptions options,
        string topicPrefix,
        ILogger<MqttTelemetryPublisher>? logger,
        bool ownsClient)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _topicPrefix = (topicPrefix ?? throw new ArgumentNullException(nameof(topicPrefix))).TrimEnd('/');
        _logger = logger;
        _ownsClient = ownsClient;
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

        return new MqttTelemetryPublisher(client, options, topicPrefix, logger, ownsClient: true);
    }

    internal static MqttClientOptions BuildClientOptions(MqttOptions opt)
    {
        ArgumentNullException.ThrowIfNull(opt);
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(opt.Host, opt.Port)
            .WithClientId(opt.ClientId)
            .WithCleanSession(true);

        if (!string.IsNullOrEmpty(opt.Username))
        {
            builder.WithCredentials(opt.Username, opt.Password);
        }

        if (opt.UseTls)
        {
            builder.WithTlsOptions(tls =>
            {
                tls.UseTls(true);
                if (opt.AllowUntrustedCertificates)
                {
                    tls.WithAllowUntrustedCertificates(true);
                    tls.WithIgnoreCertificateChainErrors(true);
                    tls.WithIgnoreCertificateRevocationErrors(true);
                    tls.WithCertificateValidationHandler(_ => true);
                }
            });
        }

        return builder.Build();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        if (!_client.IsConnected)
        {
            await _client.ConnectAsync(_options, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 단일 텔레메트리 메트릭을 JSON 형태로 MQTT 브로커에 비동기 발행합니다.
    /// </summary>
    public async Task PublishMetricAsync(
        TelemetryMetric metric,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        ArgumentNullException.ThrowIfNull(metric);
        ValidatePublishTopicSegment(metric.Name, nameof(metric));
        if (double.IsNaN(metric.Value) || double.IsInfinity(metric.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(metric), metric.Value, "TelemetryMetric.Value must be a finite number.");
        }

        string topic = $"{_topicPrefix}/{metric.Name.TrimStart('/')}";
        if (!_client.IsConnected)
        {
            HandleDisconnectedPublish(qos, topic);
            return;
        }

        using var stream = new MemoryStream();
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
    /// </summary>
    public async Task PublishRawAsync(
        string subTopic,
        ReadOnlyMemory<byte> payload,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        ValidatePublishTopicSegment(subTopic, nameof(subTopic));

        string topic = $"{_topicPrefix}/{subTopic.TrimStart('/')}";
        if (!_client.IsConnected)
        {
            HandleDisconnectedPublish(qos, topic);
            return;
        }

        var builder = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithQualityOfServiceLevel(qos);

        if (MemoryMarshal.TryGetArray(payload, out ArraySegment<byte> segment))
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

    private void HandleDisconnectedPublish(MqttQualityOfServiceLevel qos, string topic)
    {
        if (qos != MqttQualityOfServiceLevel.AtMostOnce)
        {
            throw new DeviceDisconnectedException($"Cannot publish MQTT message to '{topic}' with QoS {qos} while disconnected.");
        }

        Interlocked.Increment(ref _droppedWhileDisconnected);
        _logger?.LogWarning("Dropping QoS 0 MQTT message to '{Topic}' because MQTT client is disconnected.", topic);
    }

    private static void ValidatePublishTopicSegment(string segment, string paramName)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new ArgumentException("MQTT publish topic segment must not be null or whitespace.", paramName);
        }

        if (segment.IndexOfAny(['+', '#', '\0']) >= 0)
        {
            throw new ArgumentException($"MQTT publish topic segment '{segment}' must not contain wildcards ('+', '#') or NUL characters.", paramName);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetime)
        {
            return new ValueTask(_disposal ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        Interlocked.Exchange(ref _isDisposed, 1);

        if (!_ownsClient)
        {
            return;
        }

        if (_client.IsConnected)
        {
            try
            {
                await _client.DisconnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger?.LogWarning(ex, "MQTT disconnect error during publisher shutdown.");
                System.Diagnostics.Debug.WriteLine($"[MqttTelemetryPublisher] Disconnect error on shutdown: {ex.Message}");
            }
        }

        _client.Dispose();
    }
}
