namespace Kable.SharedMemory;

using System.IO.Pipes;

public sealed partial class SharedMemoryConnectionContext
{
    private PipeStream? _controlPipe;
    private Task _controlTask = Task.CompletedTask;

    /// <summary>The generation shared by both managed endpoints; null for raw endpoints.</summary>
    public Guid? SessionGeneration { get; private set; }

    internal void AttachControlPipe(PipeStream pipe, Guid generation)
    {
        lock (_lifetimeSync)
        {
            if (_cts.IsCancellationRequested || _cancellationDisposed)
                throw new IOException("The shared memory connection closed during its handshake.");
            if (_controlPipe != null) throw new InvalidOperationException("A control pipe is already attached.");
            _controlPipe = pipe;
            SessionGeneration = generation;
            _controlTask = MonitorControlAsync(pipe, _cts.Token);
        }
    }

    private async Task MonitorControlAsync(PipeStream pipe, CancellationToken ct)
    {
        try
        {
            // EOF, an unexpected byte, or a pipe error all terminate only this IPC generation.
            await pipe.ReadAsync(new byte[1], ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Local endpoint shutdown cancels its control read.
        }
        catch (IOException)
        {
            // A process crash can surface as a broken control pipe rather than EOF.
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
            // StopPumps closes the control handle after publishing cancellation.
        }
        finally { StopPumps(); }
    }
}
