namespace Kable.SharedMemory;

using System.Threading.Tasks;

public sealed partial class SharedMemoryConnectionContext
{
    private readonly object _lifetimeSync = new();
    private Task? _disposeTask;
    private bool _cancellationDisposed;

    public void Abort(string reason) => StopPumps();

    private void StopPumps()
    {
        lock (_lifetimeSync)
        {
            if (_cancellationDisposed) return;
            try { _cts.Cancel(); }
            finally
            {
                _inboundBuffer.Close();
                _outboundBuffer.Close();
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource? owner = null;
        Task disposal;
        lock (_lifetimeSync)
        {
            if (_disposeTask == null)
            {
                owner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposeTask = owner.Task;
            }
            disposal = _disposeTask;
        }
        if (owner != null) _ = DisposeCoreAsync(owner);
        return new ValueTask(disposal);
    }

    private async Task DisposeCoreAsync(TaskCompletionSource completion)
    {
        try
        {
            try
            {
                StopPumps();
                _inboundPipe.Reader.CancelPendingRead();
                _outboundPipe.Writer.CancelPendingFlush();
                await Task.WhenAll(_pumpInboundTask, _pumpOutboundTask).ConfigureAwait(false);
            }
            finally
            {
                _inboundBuffer.Dispose();
                _outboundBuffer.Dispose();
                lock (_lifetimeSync)
                {
                    _cts.Dispose();
                    _cancellationDisposed = true;
                }
            }
            completion.TrySetResult();
        }
        catch (Exception error) { completion.TrySetException(error); }
    }
}
