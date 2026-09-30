namespace Kable.Tests.Fixtures;

using System;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

public sealed class TestMemoryConnectionContext : IConnectionContext
{
    private readonly Pipe _inPipe;
    private readonly Pipe _outPipe;
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<bool> _flushStartedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly PipeWriter _outputWriter;

    public Task FlushStarted => _flushStartedTcs.Task;

    public TestMemoryConnectionContext(PipeOptions? inPipeOptions = null, PipeOptions? outPipeOptions = null)
    {
        _inPipe = inPipeOptions != null ? new Pipe(inPipeOptions) : new Pipe();
        _outPipe = outPipeOptions != null ? new Pipe(outPipeOptions) : new Pipe();
        _outputWriter = new FlushNotifyingPipeWriter(_outPipe.Writer, _flushStartedTcs);
    }

    public string ConnectionId { get; } = "TEST-MEM-CONN";
    public string EndpointDescription { get; } = "In-Memory Duplex Pipe";
    public PipeReader Input => _inPipe.Reader;
    public PipeWriter Output => _outputWriter;
    public CancellationToken ConnectionClosed => _cts.Token;

    public PipeReader RemoteRead => _outPipe.Reader;
    public PipeWriter RemoteWrite => _inPipe.Writer;

    public void Abort(string reason)
    {
        _cts.Cancel();
        _inPipe.Reader.Complete(new OperationCanceledException(reason));
        _outPipe.Writer.Complete(new OperationCanceledException(reason));
    }

    public async Task WriteFragmentedBytesAsync(byte[] data, int chunkSize, TimeSpan delayBetweenChunks)
    {
        try
        {
            for (int i = 0; i < data.Length; i += chunkSize)
            {
                int len = Math.Min(chunkSize, data.Length - i);
                var slice = data.AsMemory(i, len);
                await RemoteWrite.WriteAsync(slice);
                await RemoteWrite.FlushAsync();
                if (delayBetweenChunks > TimeSpan.Zero)
                {
                    await Task.Delay(delayBetweenChunks);
                }
            }
        }
        catch (Exception)
        {
            // Drop writes if pipe is closed or aborted
        }
    }

    public async Task WriteAsciiLineAsync(string text, byte delimiter = 0x0A)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await RemoteWrite.WriteAsync(new ReadOnlyMemory<byte>(bytes));
            await RemoteWrite.WriteAsync(new ReadOnlyMemory<byte>(new byte[] { delimiter }));
            await RemoteWrite.FlushAsync();
        }
        catch (Exception)
        {
            // Drop writes if pipe is closed or aborted
        }
    }

    private int _isDisposed;
    public bool IsDisposed => Volatile.Read(ref _isDisposed) == 1;

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _cts.Cancel();
            _inPipe.Writer.Complete();
            _inPipe.Reader.Complete();
            _outPipe.Writer.Complete();
            _outPipe.Reader.Complete();
            _cts.Dispose();
        }
        return default;
    }
}

public sealed class TestMemoryConnectionFactory : IConnectionFactory
{
    public TestMemoryConnectionContext Context { get; }

    public TestMemoryConnectionFactory(PipeOptions? inPipeOptions = null, PipeOptions? outPipeOptions = null)
    {
        Context = new TestMemoryConnectionContext(inPipeOptions, outPipeOptions);
    }

    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new ValueTask<IConnectionContext>(Context);
}

internal sealed class FlushNotifyingPipeWriter : PipeWriter
{
    private readonly PipeWriter _inner;
    private readonly TaskCompletionSource<bool> _tcs;

    public FlushNotifyingPipeWriter(PipeWriter inner, TaskCompletionSource<bool> tcs)
    {
        _inner = inner;
        _tcs = tcs;
    }

    public override void Advance(int bytes) => _inner.Advance(bytes);
    public override Memory<byte> GetMemory(int sizeHint = 0) => _inner.GetMemory(sizeHint);
    public override Span<byte> GetSpan(int sizeHint = 0) => _inner.GetSpan(sizeHint);
    public override void CancelPendingFlush() => _inner.CancelPendingFlush();
    public override void Complete(Exception? exception = null) => _inner.Complete(exception);

    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        _tcs.TrySetResult(true);
        return _inner.FlushAsync(cancellationToken);
    }
}
