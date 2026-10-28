namespace Kable.Modbus.Rtu;

using System.Diagnostics;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Modbus.Messages;
using Kable.Observability;

/// <summary>Serializes the whole RTU exchange and applies the silent interval before each request.</summary>
internal sealed class ModbusRtuRequestSession : IDeviceSession<ModbusTcpMessage>
{
    private readonly IDeviceSession<ModbusTcpMessage> _inner;
    private readonly TimeSpan _frameDelay;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly CancellationToken _shutdownToken;
    private long _lastCompleted = Stopwatch.GetTimestamp();
    private int _disposed;
    internal ModbusRtuRequestSession(IDeviceSession<ModbusTcpMessage> inner, TimeSpan frameDelay)
    {
        _inner = inner;
        _frameDelay = frameDelay;
        _shutdownToken = _shutdown.Token;
    }
    public bool IsConnected => Volatile.Read(ref _disposed) == 0 && _inner.IsConnected;
    public CommHealthMetrics HealthMetrics => _inner.HealthMetrics;
    public IAsyncEnumerable<ModbusTcpMessage> Stream => _inner.Stream;
    public async ValueTask StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _inner.StartAsync(ct).ConfigureAwait(false);
        Volatile.Write(ref _lastCompleted, Stopwatch.GetTimestamp());
    }

    public async ValueTask<TResponse> RequestAsync<TResponse>(ModbusTcpMessage request, TimeSpan timeout, CancellationToken ct = default)
    {
        ModbusRtuOptions.ValidateUnit(request.UnitId);
        ModbusRtuOptions.ValidateTimeout(timeout);
        ct.ThrowIfCancellationRequested();
        if (!IsConnected) throw new DeviceDisconnectedException("RTU session is not connected.");
        long started = Stopwatch.GetTimestamp();
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token, _shutdownToken);
        bool entered = false;
        try
        {
            await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
            entered = true;
            if (!IsConnected) throw new DeviceDisconnectedException("RTU session is not connected.");
            TimeSpan remainingGap;
            while ((remainingGap = _frameDelay - Stopwatch.GetElapsedTime(Volatile.Read(ref _lastCompleted))) > TimeSpan.Zero)
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Ceiling(remainingGap.TotalMilliseconds)), linked.Token).ConfigureAwait(false);
            TimeSpan remaining = timeout - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero) throw new DeviceTimeoutException(request.ToString(), timeout);
            try
            {
                return await _inner.RequestAsync<TResponse>(request, remaining, linked.Token).ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref _lastCompleted, Stopwatch.GetTimestamp());
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new DeviceTimeoutException(request.ToString(), timeout);
        }
        finally
        {
            if (entered) _gate.Release();
        }
    }

    public ValueTask SendAsync(ModbusTcpMessage message, CancellationToken ct = default) =>
        throw new NotSupportedException("RTU Master exposes request/response operations only.");
    public ValueTask SendUrgentAsync(ModbusTcpMessage message) =>
        throw new NotSupportedException("RTU does not permit bypassing an active exchange.");
    public ValueTask StopAsync() => _inner.StopAsync();
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _shutdown.Cancel();
        await _inner.DisposeAsync().ConfigureAwait(false);
        _shutdown.Dispose();
        // Asynchronous waiters may still be releasing the gate; it has no allocated OS wait handle.
    }
    public void Dispose() => throw new NotSupportedException("Use DisposeAsync for RTU sessions.");
}
