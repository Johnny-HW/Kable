namespace Kable.Transports;

using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

public sealed class SerialPortConnectionFactory : IConnectionFactory
{
    private readonly Func<CancellationToken, Task<IConnectionContext>> _open;

    public SerialPortConnectionFactory(string portName, int baudRate = 9600, Parity parity = Parity.None,
        int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        _open = ct => Task.Run<IConnectionContext>(() =>
        {
            ct.ThrowIfCancellationRequested();
            var port = new SerialPort(portName, baudRate, parity, dataBits, stopBits) { DtrEnable = true, RtsEnable = true };
            try
            {
                port.Open();
                ct.ThrowIfCancellationRequested();
                return new SerialPortConnectionContext(port);
            }
            catch
            {
                port.Dispose();
                throw;
            }
        }, ct);
    }

    internal SerialPortConnectionFactory(Func<CancellationToken, Task<IConnectionContext>> open) => _open = open;

    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var opening = _open(ct);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (ct.Register(() => canceled.TrySetResult(true)))
        {
            if (await Task.WhenAny(opening, canceled.Task).ConfigureAwait(false) != opening || ct.IsCancellationRequested)
            {
                _ = CleanupLateConnectionAsync(opening);
                throw new OperationCanceledException(ct);
            }
        }
        return await opening.ConfigureAwait(false);
    }

    private static async Task CleanupLateConnectionAsync(Task<IConnectionContext> opening)
    {
        try { await (await opening.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false); }
        catch (OperationCanceledException) { /* Opening honored the caller's cancellation. */ }
        catch (Exception error) { Trace.TraceError("Canceled serial open cleanup: {0}", error); }
    }
}
