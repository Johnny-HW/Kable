namespace Kable.OpcUa.Subscriptions;

using System.Threading.Channels;
using Kable.OpcUa.Models;
using Opc.Ua;
using Opc.Ua.Client;

public sealed class OpcUaValueSubscription : IAsyncDisposable
{
    private readonly string[] _nodes;
    private readonly OpcUaSubscriptionOptions _options;
    private readonly Func<OpcUaValueSubscription, Task> _remove;
    private readonly Channel<OpcUaDataValue> _values;
    private Subscription? _subscription;
    private long _dropped;
    private int _disposed;
    public ChannelReader<OpcUaDataValue> Values => _values.Reader;
    public long DroppedValues => Interlocked.Read(ref _dropped);
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    internal OpcUaValueSubscription(string[] nodes, OpcUaSubscriptionOptions options, Func<OpcUaValueSubscription, Task> remove)
    {
        if (nodes.Length == 0 || nodes.Distinct(StringComparer.Ordinal).Count() != nodes.Length) throw new ArgumentException("Unique node IDs are required.", nameof(nodes));
        if (options.Capacity <= 0 || options.PublishingIntervalMs <= 0 || options.SamplingIntervalMs < 0 || options.ServerQueueSize == 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        foreach (string node in nodes) NodeId.Parse(node);
        _nodes = nodes; _options = options; _remove = remove;
        _values = Channel.CreateBounded<OpcUaDataValue>(new BoundedChannelOptions(options.Capacity)
        { FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });
    }

    internal async Task AttachAsync(Session session, CancellationToken ct)
    {
        if (IsDisposed) return;
        Detach();
        var subscription = new Subscription(session.DefaultSubscription)
        { PublishingInterval = _options.PublishingIntervalMs, PublishingEnabled = true };
        _subscription = subscription;
        foreach (string node in _nodes)
        {
            var item = new MonitoredItem(subscription.DefaultItem)
            {
                StartNodeId = NodeId.Parse(node), AttributeId = Attributes.Value, DisplayName = node,
                SamplingInterval = _options.SamplingIntervalMs, QueueSize = _options.ServerQueueSize,
                DiscardOldest = true, MonitoringMode = MonitoringMode.Reporting
            };
            item.Notification += OnNotification;
            subscription.AddItem(item);
        }
        session.AddSubscription(subscription);
        await subscription.CreateAsync(ct).ConfigureAwait(false);
        if (subscription.MonitoredItems.Any(item => ServiceResult.IsBad(item.Status.Error)))
            throw new InvalidOperationException("OPC UA server rejected a monitored item.");
    }

    private void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs args)
    {
        foreach (var value in item.DequeueValues())
            if (!_values.Writer.TryWrite(new OpcUaDataValue(item.DisplayName, value.Value, value.StatusCode.Code, value.SourceTimestamp)))
                Interlocked.Increment(ref _dropped);
    }
    private void Detach()
    {
        if (_subscription == null) return;
        foreach (var item in _subscription.MonitoredItems) item.Notification -= OnNotification;
        _subscription.Dispose();
        _subscription = null;
    }
    internal async Task CloseAsync(CancellationToken ct)
    {
        try
        {
            if (_subscription?.Created == true && _subscription.Session?.Connected == true)
                await _subscription.Session.RemoveSubscriptionAsync(_subscription, ct).ConfigureAwait(false);
        }
        finally { try { Detach(); } finally { _values.Writer.TryComplete(); } }
    }
    internal void Complete()
    {
        Interlocked.Exchange(ref _disposed, 1);
        try { Detach(); } finally { _values.Writer.TryComplete(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) await _remove(this).ConfigureAwait(false);
    }
}
