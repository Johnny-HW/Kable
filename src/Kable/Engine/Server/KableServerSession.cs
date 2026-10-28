namespace Kable.Engine;

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;

/// <summary>A bounded outgoing queue and one connection's lifetime.</summary>
internal sealed class KableServerSession<TMessage> : IServerSession<TMessage>
{
    private readonly IConnectionContext _context;
    private readonly IProtocolCodec<TMessage> _codec;
    private readonly Channel<TMessage> _sendQueue;
    private readonly CancellationTokenSource _sessionCts = new();
    private readonly CancellationToken _sessionClosed;
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _lifetime = new();
    private Task? _disposeTask;
    private int _disconnected;
    private int _sendingCompleted;

    public string ConnectionId => _context.ConnectionId;
    public string EndpointDescription => _context.EndpointDescription;
    public bool IsConnected => Volatile.Read(ref _disconnected) == 0 && !_context.ConnectionClosed.IsCancellationRequested;
    internal IConnectionContext ConnectionContext => _context;
    internal IProtocolCodec<TMessage> Codec => _codec;
    internal CancellationToken SessionClosed => _sessionClosed;
    internal Task Completion => _completion.Task;
    internal void MarkCompleted() => _completion.TrySetResult(true);
    internal void CompleteSending()
    {
        Interlocked.Exchange(ref _sendingCompleted, 1);
        _sendQueue.Writer.TryComplete();
    }

    public KableServerSession(IConnectionContext context, IProtocolCodec<TMessage> codec, int queueCapacity = 1_000)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        if (queueCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        _sessionClosed = _sessionCts.Token;
        _sendQueue = Channel.CreateBounded<TMessage>(new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false
        });
    }

    public async ValueTask SendAsync(TMessage message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsConnected || Volatile.Read(ref _sendingCompleted) != 0)
            throw new InvalidOperationException($"Session {ConnectionId} is not accepting messages.");
        if (_sendQueue.Writer.TryWrite(message)) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _sessionClosed, _context.ConnectionClosed);
        try { await _sendQueue.Writer.WriteAsync(message, linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException($"Session {ConnectionId} closed while queuing message.");
        }
        catch (ChannelClosedException error)
        {
            throw new InvalidOperationException($"Session {ConnectionId} stopped accepting messages.", error);
        }
    }

    public void Disconnect(string reason)
    {
        lock (_lifetime)
        {
            if (Interlocked.Exchange(ref _disconnected, 1) != 0) return;
            CompleteSending();
            _sessionCts.Cancel();
            _context.Abort(reason);
        }
    }

    internal async Task RunSendLoopAsync()
    {
        var reader = _sendQueue.Reader;
        var writer = _context.Output;
        try
        {
            while (await reader.WaitToReadAsync(_sessionClosed).ConfigureAwait(false))
            {
                while (reader.TryRead(out var message))
                {
                    _codec.Encode(message, writer);
                    var flush = await writer.FlushAsync(_sessionClosed).ConfigureAwait(false);
                    if (flush.IsCompleted || flush.IsCanceled) return;
                }
            }
        }
        catch (OperationCanceledException) when (_sessionClosed.IsCancellationRequested) { }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetime)
        {
            return new ValueTask(_disposeTask ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        try { Disconnect("Session disposed"); }
        finally
        {
            try { await _context.DisposeAsync().ConfigureAwait(false); }
            finally { _sessionCts.Dispose(); }
        }
    }
}
