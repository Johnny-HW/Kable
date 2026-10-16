namespace Kable.Tests.Fixtures.Engine;

using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Tests.Fixtures;

internal sealed class TrackingDisposableConnectionContext : IConnectionContext
{
    private readonly TestMemoryConnectionContext _inner = new();
    private int _disposeCallCount;
    public int DisposeCallCount => Volatile.Read(ref _disposeCallCount);

    public string ConnectionId => _inner.ConnectionId;
    public string EndpointDescription => _inner.EndpointDescription;
    public System.IO.Pipelines.PipeReader Input => _inner.Input;
    public System.IO.Pipelines.PipeWriter Output => _inner.Output;
    public CancellationToken ConnectionClosed => _inner.ConnectionClosed;

    public void Abort(string reason) => _inner.Abort(reason);
    public Task WriteAsciiLineAsync(string text, byte delimiter = 0x0A) => _inner.WriteAsciiLineAsync(text, delimiter);

    public async ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeCallCount);
        await _inner.DisposeAsync();
    }
}
