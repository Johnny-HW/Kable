namespace Kable.Tests.Fixtures;

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Kable.Core;

public sealed class TestMemoryConnectionListener : IConnectionListener
{
    private readonly Channel<IConnectionContext> _channel = Channel.CreateUnbounded<IConnectionContext>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private int _isStopped;

    public bool IsStopped => Volatile.Read(ref _isStopped) == 1;

    public void EnqueueConnection(IConnectionContext context)
    {
        _channel.Writer.TryWrite(context);
    }

    public async ValueTask<IConnectionContext> AcceptAsync(CancellationToken ct = default)
    {
        try
        {
            return await _channel.Reader.ReadAsync(ct).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            throw new OperationCanceledException("Listener is stopped.");
        }
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _isStopped, 1) == 0)
        {
            _channel.Writer.TryComplete();
        }
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }
}
