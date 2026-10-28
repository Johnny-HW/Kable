namespace Kable.Transports.Lifecycle;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Separates immediate transport abort from exactly-once asynchronous pipeline cleanup.</summary>
internal sealed class StreamConnectionLifetime
{
    private readonly Stream _stream;
    private readonly Action _closeTransport;
    private readonly PipeReader _input;
    private readonly PipeWriter _output;
    private readonly CancellationTokenSource _closed = new();
    private readonly object _sync = new();
    private Task? _disposal;
    private int _aborted;
    internal CancellationToken Closed { get; }

    internal StreamConnectionLifetime(Stream stream, PipeReader input, PipeWriter output, Action closeTransport)
    {
        _stream = stream; _input = input; _output = output; _closeTransport = closeTransport;
        Closed = _closed.Token;
    }

    internal void Abort()
    {
        if (Interlocked.Exchange(ref _aborted, 1) != 0) return;
        try { _closed.Cancel(); }
        finally
        {
            try { _closeTransport(); }
            finally
            {
                try { _input.CancelPendingRead(); }
                finally { _output.CancelPendingFlush(); }
            }
        }
    }

    internal ValueTask DisposeAsync()
    {
        lock (_sync) return new ValueTask(_disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        var errors = new List<Exception>();
        try { Abort(); } catch (Exception error) { errors.Add(error); }
        try { await _input.CompleteAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
        try { await _output.CompleteAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
        try
        {
#if NETSTANDARD2_0
            _stream.Dispose();
#else
            await _stream.DisposeAsync().ConfigureAwait(false);
#endif
        }
        catch (Exception error) { errors.Add(error); }
        finally { _closed.Dispose(); }
        if (errors.Count > 0) throw new AggregateException("Connection cleanup failed after releasing all resources.", errors);
    }
}
