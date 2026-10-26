namespace Kable.Channels;

using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Exceptions;
using Kable.Transports;

/// <summary>Serialized Modbus RTU channel with safe-close after an in-flight failure.</summary>
public sealed class KableModbusDeviceChannel : IKableModbusChannel
{
    private readonly IConnectionFactory _factory;
    private readonly ModbusRtuCodec _codec = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateSync = new();
    private readonly string _unitId;
    private IConnectionContext? _context;
    private CancellationTokenSource? _active;
    private Task? _disposeTask;
    private bool _disposed;
    private int _closing;

    public bool IsOpen
    {
        get
        {
            lock (_stateSync)
                return _context is { } context && !context.ConnectionClosed.IsCancellationRequested;
        }
    }
    public string UnitId => _unitId;

    public KableModbusDeviceChannel(string portName, int baudRate = 19200,
        Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One,
        string unitId = "ModbusDevice")
        : this(new SerialPortConnectionFactory(portName, baudRate, parity, dataBits, stopBits), unitId) { }

    public KableModbusDeviceChannel(IConnectionFactory factory, string unitId = "ModbusDevice")
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _unitId = unitId;
    }

    private CancellationTokenSource BeginOperation(CancellationToken ct)
    {
        lock (_stateSync)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KableModbusDeviceChannel));
            if (_closing != 0) throw new DeviceDisconnectedException("Channel is closing.");
            return _active = CancellationTokenSource.CreateLinkedTokenSource(ct);
        }
    }

    private void EndOperation()
    {
        lock (_stateSync)
        {
            _active?.Dispose();
            _active = null;
        }
    }

    public async Task OpenAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var operation = BeginOperation(ct);
            if (_context != null && !_context.ConnectionClosed.IsCancellationRequested) return;
            await ReleaseContextAsync(false).ConfigureAwait(false);
            var context = await _factory.ConnectAsync(operation.Token).ConfigureAwait(false);
            lock (_stateSync) _context = context;
            if (operation.IsCancellationRequested)
            {
                await ReleaseContextAsync(true).ConfigureAwait(false);
                operation.Token.ThrowIfCancellationRequested();
            }
        }
        finally { EndOperation(); _gate.Release(); }
    }

    public Task CloseAsync(CancellationToken ct = default) => CloseCoreAsync(ct, false);

    private async Task CloseCoreAsync(CancellationToken ct, bool disposing)
    {
        ct.ThrowIfCancellationRequested();
        lock (_stateSync)
        {
            if (disposing) _disposed = true;
            _closing++;
            _active?.Cancel();
        }
        bool entered = false;
        try
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            entered = true;
            await ReleaseContextAsync(false).ConfigureAwait(false);
        }
        finally
        {
            lock (_stateSync) _closing--;
            if (entered) _gate.Release();
        }
    }

    [Obsolete("Use OpenAsync instead to avoid UI deadlocks.")]
    public void Open() => OpenAsync().GetAwaiter().GetResult();

    [Obsolete("Use CloseAsync instead to avoid UI deadlocks.")]
    public void Close() => CloseAsync().GetAwaiter().GetResult();

    public async ValueTask<byte[]> SendAndReceiveFrameAsync(byte[] requestPdu, TimeSpan timeout,
        CancellationToken ct = default)
    {
        if (requestPdu == null) throw new ArgumentNullException(nameof(requestPdu));
        ct.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(timeout);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        bool entered = false;
        bool started = false;
        try
        {
            await _gate.WaitAsync(wait.Token).ConfigureAwait(false);
            entered = true;
            var operation = BeginOperation(wait.Token);
            var context = _context ?? throw new InvalidOperationException(
                $"Channel is not open for unit '{_unitId}'. Call OpenAsync() first.");
            using var io = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, context.ConnectionClosed);
            io.Token.ThrowIfCancellationRequested();
            started = true;
            _codec.Encode(requestPdu, context.Output);
            var flush = await context.Output.FlushAsync(io.Token).ConfigureAwait(false);
            if (flush.IsCanceled) throw new OperationCanceledException(io.Token);
            if (flush.IsCompleted) throw new DeviceDisconnectedException("Modbus output completed.");
            while (true)
            {
                var result = await context.Input.ReadAsync(io.Token).ConfigureAwait(false);
                var buffer = result.Buffer;
                try
                {
                    if (result.IsCanceled) throw new OperationCanceledException(io.Token);
                    if (_codec.TryDecode(ref buffer, out var response)) return response.ToArray();
                    if (result.IsCompleted) throw new DeviceDisconnectedException("Modbus input completed.");
                }
                finally { context.Input.AdvanceTo(buffer.Start, buffer.End); }
            }
        }
        catch (OperationCanceledException ex)
        {
            if (started) await ReleaseContextAsync(true).ConfigureAwait(false);
            if (ct.IsCancellationRequested) throw new OperationCanceledException(ex.Message, ex, ct);
            if (deadline.IsCancellationRequested) throw new TimeoutException("Modbus request timed out.", ex);
            throw new DeviceDisconnectedException("Modbus connection was closed.", ex);
        }
        catch (Exception ex)
        {
            if (started) await ReleaseContextAsync(true).ConfigureAwait(false);
            if (started && (ex is IOException || ex is ObjectDisposedException))
                throw new DeviceDisconnectedException("Modbus connection failed.", ex);
            throw;
        }
        finally
        {
            if (entered) { EndOperation(); _gate.Release(); }
        }
    }

    private async Task ReleaseContextAsync(bool preserveFailure)
    {
        IConnectionContext? context;
        lock (_stateSync)
        {
            context = _context;
            _context = null;
        }
        if (context == null) return;
        Exception? abortError = null;
        try { context.Abort("Modbus channel closed."); }
        catch (Exception ex) { abortError = ex; }
        try { await context.DisposeAsync().ConfigureAwait(false); }
        catch (Exception) when (preserveFailure) { return; }
        if (abortError != null && !preserveFailure) throw abortError;
    }

    public ValueTask DisposeAsync()
    {
        lock (_stateSync)
            return new ValueTask(_disposeTask ??= CloseCoreAsync(CancellationToken.None, true));
    }
}
