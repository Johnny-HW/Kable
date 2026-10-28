namespace Kable.Transports;

using System;
using System.IO.Pipelines;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

public sealed class SerialPortConnectionContext : IConnectionContext
{
    private readonly SerialPort _port;
    private readonly CancellationTokenSource _cts = new();
    private int _aborted;
    private Task? _disposeTask;
    private readonly object _lifetime = new();
    private readonly System.IO.Stream _stream;
    private readonly bool _ownsStream;

    public string ConnectionId { get; }
    public string EndpointDescription { get; }
    public PipeReader Input { get; }
    public PipeWriter Output { get; }
    public CancellationToken ConnectionClosed { get; }

    public SerialPortConnectionContext(SerialPort port)
        : this(port, port.BaseStream, false)
    {
    }

    internal SerialPortConnectionContext(SerialPort port, System.IO.Stream stream, bool ownsStream = true)
    {
        _port = port;
        _stream = stream;
        _ownsStream = ownsStream;
        ConnectionId = Guid.NewGuid().ToString("N");
        EndpointDescription = $"Serial {port.PortName} ({port.BaudRate},{port.DataBits},{port.Parity},{port.StopBits})";
        ConnectionClosed = _cts.Token;

        Input = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
        Output = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
    }

    public void Abort(string reason)
    {
        if (Interlocked.Exchange(ref _aborted, 1) == 0)
        {
            _cts.Cancel();
            try { _port.Close(); } catch (Exception) { /* Serial port already closed or physical cable unplugged */ }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetime) return new ValueTask(_disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        Abort("Disposed");
        try
        {
            await Input.CompleteAsync().ConfigureAwait(false);
        }
        finally
        {
            try { await Output.CompleteAsync().ConfigureAwait(false); }
            finally
            {
                try { if (_ownsStream) _stream.Dispose(); }
                finally { _port.Dispose(); _cts.Dispose(); }
            }
        }
    }
}
