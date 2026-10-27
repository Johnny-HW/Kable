namespace Kable.SharedMemory.Connections;

public sealed partial class SharedMemoryServerListener
{
    public void Stop()
    {
        lock (_sync)
        {
            if (_stopRequested) return;
            _stopRequested = true;
            _stop.Cancel();
            _pending?.Dispose();
            _active?.Abort("Shared memory listener stopped.");
        }
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource? owner = null;
        Task disposal;
        lock (_sync)
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
            Stop();
            await _acceptGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_active != null) await _active.DisposeAsync().ConfigureAwait(false);
                _pending?.Dispose();
            }
            finally
            {
                _owner.Dispose();
                _stop.Dispose();
                _acceptGate.Release();
            }
            completion.TrySetResult();
        }
        catch (Exception error) { completion.TrySetException(error); }
    }
}
