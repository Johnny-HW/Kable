namespace Kable.Engine.Messaging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;

/// <summary>Owns request reservations and reply matching, independently of transport and stream delivery.</summary>
internal sealed class ResponseRouter<TMessage> : IDisposable
{
    private readonly IProtocolCodec<TMessage> _codec;
    private readonly SemaphoreSlim _fifo = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TMessage>> _pending = new();
    private TaskCompletionSource<TMessage>? _current;
    private Exception? _terminalReason;

    internal ResponseRouter(IProtocolCodec<TMessage> codec) => _codec = codec;
    internal Task WaitForFifoAsync(CancellationToken ct) => _fifo.WaitAsync(ct);
    internal TaskCompletionSource<TMessage> BeginFifo()
    {
        var reservation = NewReservation();
        Volatile.Write(ref _current, reservation);
        var reason = Volatile.Read(ref _terminalReason);
        if (reason != null) reservation.TrySetException(reason);
        return reservation;
    }
    internal void EndFifo() { Volatile.Write(ref _current, null); _fifo.Release(); }
    internal TaskCompletionSource<TMessage> Reserve(string cid)
    {
        var reservation = NewReservation();
        if (!_pending.TryAdd(cid, reservation))
            throw new InvalidOperationException($"Duplicate in-flight Correlation ID '{cid}' detected.");
        var reason = Volatile.Read(ref _terminalReason);
        if (reason != null) { Release(cid, reservation); reservation.TrySetException(reason); }
        return reservation;
    }
    internal void Release(string cid, TaskCompletionSource<TMessage> owner) =>
        ((ICollection<KeyValuePair<string, TaskCompletionSource<TMessage>>>)_pending)
            .Remove(new KeyValuePair<string, TaskCompletionSource<TMessage>>(cid, owner));

    internal bool TryRoute(TMessage message)
    {
        if (_codec.SupportsCorrelationId)
        {
            var cid = _codec.ExtractCorrelationId(message);
            return cid != null && _pending.TryRemove(cid, out var reservation) && reservation.TrySetResult(message);
        }
        return Volatile.Read(ref _current)?.TrySetResult(message) == true;
    }
    internal void FailAll(Exception reason)
    {
        Interlocked.CompareExchange(ref _terminalReason, reason, null);
        Volatile.Read(ref _current)?.TrySetException(reason);
        foreach (var reservation in _pending) reservation.Value.TrySetException(reason);
        _pending.Clear();
    }
    private static TaskCompletionSource<TMessage> NewReservation() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Dispose() => _fifo.Dispose();
}
