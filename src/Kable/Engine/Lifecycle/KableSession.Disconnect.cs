namespace Kable.Engine;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Exceptions;
using Kable.Observability;

public sealed partial class KableSession<TMessage>
{
    private Exception? _backgroundCleanupError;
    /// <summary>Failure observed during automatic cleanup; StopAsync also reports the shared failure.</summary>
    public Exception? LastCleanupError => Volatile.Read(ref _backgroundCleanupError);
    private void OnConnectionClosed()
    {
        OnConnectionClosed(null);
    }

    private void OnConnectionClosed(Exception? specificReason)
    {
        if (Interlocked.Exchange(ref _isConnected, 0) == 1)
        {
            var ex = specificReason ?? new DeviceDisconnectedException("Hardware connection has been disconnected. (Fail-fast aborting all pending requests)");
            _disconnectReason = ex;
            _healthTracker.SetState(specificReason != null ? DeviceConnectionState.Faulted : DeviceConnectionState.Disconnected);

            _responses.FailAll(ex);
            _alarmStream.Writer.TryComplete(ex);
            _delivery.Complete(ex);
            _outboundNormalQueue.Writer.TryComplete(ex);
            _outboundUrgentQueue.Writer.TryComplete(ex);

            while (_outboundUrgentQueue.Reader.TryRead(out var pendingUrgent))
            {
                pendingUrgent.Completion.TrySetException(ex);
            }
            while (_outboundNormalQueue.Reader.TryRead(out var pendingNormal))
            {
                pendingNormal.Completion.TrySetException(ex);
            }

            try
            {
                _sessionCts.Cancel();
            }
            catch (ObjectDisposedException) { }

            try
            {
                _context?.Abort("Hardware connection has been disconnected.");
            }
            catch { }
            QueueCleanup(specificReason is AlarmBufferOverflowException ? specificReason : null);
        }
    }

    private void QueueCleanup(Exception? reason)
    {
        _ = Task.Run(async () =>
        {
            try { await PerformCleanupAsync(reason).ConfigureAwait(false); }
            catch (Exception cleanupError)
            {
                // StopAsync/DisposeAsync still expose the shared cleanup failure to callers.
                Volatile.Write(ref _backgroundCleanupError, cleanupError);
            }
        });
    }
}
