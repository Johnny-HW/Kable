namespace Kable.Engine.Streaming;

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Kable.Exceptions;

/// <summary>Owns telemetry buffering; response routing never awaits a stream consumer.</summary>
internal sealed class InboundDelivery<TMessage>
{
    private readonly Channel<TMessage>? _backlog;
    private readonly int _backlogCapacity;
    private long _dropped;
    private int _completed;
    internal Channel<TMessage> Stream { get; }
    internal long DroppedCount => Interlocked.Read(ref _dropped);

    internal InboundDelivery(int capacity, int backlogCapacity, BoundedChannelFullMode mode, Action<TMessage> onDrop)
    {
        if (backlogCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(backlogCapacity));
        _backlogCapacity = backlogCapacity;
        Stream = Channel.CreateBounded<TMessage>(new BoundedChannelOptions(capacity)
        { FullMode = mode, SingleWriter = true, AllowSynchronousContinuations = false }, item =>
        {
            Interlocked.Increment(ref _dropped);
            onDrop(item);
        });
        if (mode == BoundedChannelFullMode.Wait)
            _backlog = Channel.CreateBounded<TMessage>(new BoundedChannelOptions(backlogCapacity)
            { FullMode = BoundedChannelFullMode.Wait, SingleWriter = true, SingleReader = true, AllowSynchronousContinuations = false });
    }

    internal void Publish(TMessage message)
    {
        if (Volatile.Read(ref _completed) != 0) return;
        if (_backlog == null) { Stream.Writer.TryWrite(message); return; }
        if (!_backlog.Writer.TryWrite(message) && Volatile.Read(ref _completed) == 0)
            throw new TelemetryBufferOverflowException($"Telemetry delivery backlog capacity ({_backlogCapacity}) exceeded. Aborting instead of blocking response routing.");
    }

    internal async Task RunAsync(CancellationToken ct)
    {
        if (_backlog == null) return;
        try
        {
            while (await _backlog.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                while (_backlog.Reader.TryRead(out var message))
                    await Stream.Writer.WriteAsync(message, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* Session shutdown. */ }
        catch (ChannelClosedException) when (Volatile.Read(ref _completed) != 0) { /* Owner has already published the terminal reason. */ }
    }

    internal void Complete(Exception? error)
    {
        Interlocked.Exchange(ref _completed, 1);
        _backlog?.Writer.TryComplete(error);
        Stream.Writer.TryComplete(error);
    }
}
