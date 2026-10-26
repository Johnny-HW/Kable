using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kable.Channels;
using Kable.Core;
using Xunit;

namespace Kable.Tests.Channels;

public sealed class MockTestConnectionContext : IConnectionContext
{
    private readonly Pipe _inputPipe = new();
    private readonly Pipe _outputPipe = new();
    private readonly CancellationTokenSource _closedCts = new();

    public string ConnectionId => "MockConnection";
    public string EndpointDescription => "mock://test";
    public PipeReader Input => _inputPipe.Reader;
    public PipeWriter Output => _outputPipe.Writer;
    public CancellationToken ConnectionClosed => _closedCts.Token;

    // Helper properties for test simulation
    public PipeWriter TestInputWriter => _inputPipe.Writer;
    public PipeReader TestOutputReader => _outputPipe.Reader;

    public bool IsDisposed { get; private set; }
    public int DisposeCount { get; private set; }
    public bool FailCleanup { get; set; }

    public void Abort(string reason)
    {
        _closedCts.Cancel();
        if (FailCleanup) throw new InvalidOperationException("Injected abort failure.");
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        IsDisposed = true;
        _closedCts.Cancel();
        _inputPipe.Reader.Complete();
        _inputPipe.Writer.Complete();
        _outputPipe.Reader.Complete();
        _outputPipe.Writer.Complete();
        _closedCts.Dispose();
        if (FailCleanup) throw new InvalidOperationException("Injected disposal failure.");
        return ValueTask.CompletedTask;
    }
}

public sealed class MockTestConnectionFactory : IConnectionFactory
{
    public MockTestConnectionContext Context { get; } = new();
    public int ConnectCount { get; private set; }

    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        ConnectCount++;
        return ValueTask.FromResult<IConnectionContext>(Context);
    }
}
