namespace Kable.Mqtt.Subscriptions;

using System.Threading.Channels;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Packets;

public sealed record MqttReceivedMessage(string Topic, ReadOnlyMemory<byte> Payload, MqttQualityOfServiceLevel Qos, bool Retain);

/// <summary>Owns a client; reconnects and restores filters without retransmitting application commands.</summary>
public sealed class MqttSubscriber : IAsyncDisposable
{
    private readonly IMqttClient _client = new MqttFactory().CreateMqttClient();
    private readonly MqttClientOptions _options;
    private readonly Dictionary<string, MqttTopicFilter> _filters;
    private readonly Channel<MqttReceivedMessage> _messages;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly TimeSpan _retryDelay;
    private readonly object _lifetime = new();
    private Task? _worker, _disposal;
    private long _received, _dropped;
    private int _ready, _disposed;
    public bool IsConnected => Volatile.Read(ref _ready) != 0 && _client.IsConnected;
    public Exception? LastError { get; private set; }
    public long ReceivedMessages => Interlocked.Read(ref _received);
    public long DroppedMessages => Interlocked.Read(ref _dropped);
    public ChannelReader<MqttReceivedMessage> Messages => _messages.Reader;

    public MqttSubscriber(MqttClientOptions options, IEnumerable<MqttTopicFilter> filters, int capacity = 1024, TimeSpan? retryDelay = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _options = options;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        if (_retryDelay <= TimeSpan.Zero || _retryDelay.TotalMilliseconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(retryDelay));
        _filters = filters.ToDictionary(f => f.Topic, StringComparer.Ordinal);
        _messages = Channel.CreateBounded<MqttReceivedMessage>(new BoundedChannelOptions(capacity)
        { FullMode = BoundedChannelFullMode.Wait, SingleWriter = true, AllowSynchronousContinuations = false });
        _client.ApplicationMessageReceivedAsync += OnMessageAsync;
    }

    private Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var message = args.ApplicationMessage;
        var owned = new MqttReceivedMessage(message.Topic, message.PayloadSegment.ToArray(), message.QualityOfServiceLevel, message.Retain);
        if (!_messages.Writer.TryWrite(owned)) Interlocked.Increment(ref _dropped);
        Interlocked.Increment(ref _received);
        return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (_worker != null) return;
            await ConnectAndSubscribeAsync(linked.Token).ConfigureAwait(false);
            _worker = RunAsync(_stop.Token);
        }
        finally { _gate.Release(); }
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken ct)
    {
        Volatile.Write(ref _ready, 0);
        if (!_client.IsConnected) await _client.ConnectAsync(_options, ct).ConfigureAwait(false);
        if (_filters.Count > 0)
        {
            var result = await _client.SubscribeAsync(new MqttClientSubscribeOptions { TopicFilters = _filters.Values.ToList() }, ct).ConfigureAwait(false);
            if (result.Items.Any(item => (int)item.ResultCode >= 128)) throw new InvalidOperationException("MQTT broker rejected a subscription.");
        }
        LastError = null;
        Volatile.Write(ref _ready, 1);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_retryDelay, ct).ConfigureAwait(false);
                if (IsConnected) continue;
                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try { await ConnectAndSubscribeAsync(ct).ConfigureAwait(false); }
                catch (Exception error) when (!ct.IsCancellationRequested) { LastError = error; }
                finally { _gate.Release(); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* Normal shutdown. */ }
    }

    public async Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (_client.IsConnected) await _client.UnsubscribeAsync(new MqttClientUnsubscribeOptions { TopicFilters = [topicFilter] }, linked.Token).ConfigureAwait(false);
            _filters.Remove(topicFilter);
        }
        finally { _gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetime) return new(_disposal ??= DisposeCoreAsync());
    }
    private async Task DisposeCoreAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        _stop.Cancel();
        if (_worker != null) await _worker.ConfigureAwait(false);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _client.ApplicationMessageReceivedAsync -= OnMessageAsync;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                if (_client.IsConnected) await _client.DisconnectAsync(new MqttClientDisconnectOptions(), timeout.Token).ConfigureAwait(false);
            }
            finally { _client.Dispose(); _messages.Writer.TryComplete(); }
        }
        finally { _gate.Release(); }
    }
}
