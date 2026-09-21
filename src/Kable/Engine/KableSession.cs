namespace Kable.Engine;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Exceptions;
using Kable.Observability;

public sealed partial class KableSession<TMessage> : IDeviceSession<TMessage>
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly IProtocolCodec<TMessage> _codec;
    private readonly ICommObserver? _observer;

    private readonly SemaphoreSlim _fifoLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TMessage>> _pendingRequests = new();

    // Telemetry/Unsolicited stream
    private readonly Channel<TMessage> _incomingStream = Channel.CreateUnbounded<TMessage>(new UnboundedChannelOptions { SingleWriter = true });

    // Inbound Dispatch Queue
    private readonly Channel<TMessage> _dispatchQueue = Channel.CreateBounded<TMessage>(new BoundedChannelOptions(10000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleWriter = true,
        SingleReader = true
    });

    // P0: Single Outbound Writer Queue (Urgent prioritised)
    private readonly Channel<OutboundCommand> _outboundUrgentQueue = Channel.CreateUnbounded<OutboundCommand>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Channel<OutboundCommand> _outboundNormalQueue = Channel.CreateBounded<OutboundCommand>(new BoundedChannelOptions(5000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });

    private IConnectionContext? _context;
    private Task? _readLoopTask;
    private Task? _dispatchLoopTask;
    private Task? _outboundPumpTask;
    private TaskCompletionSource<TMessage>? _currentFifoTcs;
    private readonly CancellationTokenSource _sessionCts = new();
    private readonly HeartbeatOptions<TMessage>? _heartbeatOptions;
    private Task? _heartbeatTask;
    private int _isConnected;
    private long _lastInboundTicks;

    public bool IsConnected => Volatile.Read(ref _isConnected) == 1;

    public async IAsyncEnumerable<TMessage> GetStreamAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token, ct);
        while (await _incomingStream.Reader.WaitToReadAsync(linkedCts.Token).ConfigureAwait(false))
        {
            while (_incomingStream.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }

    public IAsyncEnumerable<TMessage> Stream => GetStreamAsync();

    public KableSession(
        IConnectionFactory connectionFactory,
        IProtocolCodec<TMessage> codec,
        ICommObserver? observer = null,
        HeartbeatOptions<TMessage>? heartbeatOptions = null)
    {
        _connectionFactory = connectionFactory;
        _codec = codec;
        _observer = observer;
        _heartbeatOptions = heartbeatOptions;
    }

    public async ValueTask StartAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _isConnected) == 1) return;

        IConnectionContext context;
        try
        {
            context = await _connectionFactory.ConnectAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Exchange(ref _isConnected, 0);
            throw;
        }

        if (Interlocked.CompareExchange(ref _isConnected, 1, 0) != 0)
        {
            await context.DisposeAsync().ConfigureAwait(false);
            return;
        }

        _context = context;
        _context.ConnectionClosed.Register(OnConnectionClosed);
        Volatile.Write(ref _lastInboundTicks, DateTime.UtcNow.Ticks);
        _outboundPumpTask = Task.Run(OutboundPumpLoopAsync);
        _dispatchLoopTask = Task.Run(DispatchLoopAsync);
        _readLoopTask = Task.Run(ReadLoopAsync);

        if (_heartbeatOptions != null)
        {
            _heartbeatTask = Task.Run(HeartbeatLoopAsync);
        }
    }

    public async ValueTask SendAsync(TMessage message, CancellationToken ct = default)
    {
        EnsureConnected();
        var cmd = new OutboundCommand(message, isUrgent: false);
        try
        {
            await _outboundNormalQueue.Writer.WriteAsync(cmd, ct).ConfigureAwait(false);
            await cmd.Completion.Task.ConfigureAwait(false);
        }
        catch (ChannelClosedException cce)
        {
            throw new DeviceDisconnectedException("Hardware connection has been disconnected.", cce);
        }

        _observer?.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow, PacketDirection.Tx, TrafficKind.AperiodicCommand,
            "SEND", ReadOnlyMemory<byte>.Empty, message?.ToString(), TimeSpan.Zero, LogLevel.Debug));
    }

    public async ValueTask<TResponse> RequestAsync<TResponse>(TMessage request, TimeSpan timeout, CancellationToken ct = default)
    {
        EnsureConnected();
        var sw = ValueStopwatch.StartNew();

        if (!_codec.SupportsCorrelationId)
        {
            await _fifoLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var tcs = new TaskCompletionSource<TMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                _currentFifoTcs = tcs;

                var cmd = new OutboundCommand(request, isUrgent: false);
                try
                {
                    await _outboundNormalQueue.Writer.WriteAsync(cmd, ct).ConfigureAwait(false);
                    await cmd.Completion.Task.ConfigureAwait(false);
                }
                catch (ChannelClosedException cce)
                {
                    throw new DeviceDisconnectedException("Hardware connection has been disconnected.", cce);
                }

                using var timeoutCts = new CancellationTokenSource(timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                var responseTask = tcs.Task;
                var completedTask = await Task.WhenAny(responseTask, Task.Delay(Timeout.Infinite, linkedCts.Token)).ConfigureAwait(false);

                if (completedTask == responseTask)
                {
                    var response = await responseTask.ConfigureAwait(false);
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Tx, TrafficKind.AperiodicCommand,
                        "REQUEST_RESP", ReadOnlyMemory<byte>.Empty, response?.ToString(), sw.GetElapsedTime(), LogLevel.Debug));

                    if (response is TResponse typedRes) return typedRes;
                    throw new InvalidCastException($"Expected {typeof(TResponse).Name}, received {response?.GetType().Name}");
                }

                if (timeoutCts.IsCancellationRequested)
                {
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Tx, TrafficKind.SpontaneousAlarm,
                        "DEVICE_TIMEOUT", ReadOnlyMemory<byte>.Empty,
                        $"Command '{request}' timed out after {timeout.TotalMilliseconds}ms.", sw.GetElapsedTime(), LogLevel.Warning));
                    throw new DeviceTimeoutException(request?.ToString() ?? "UnknownCommand", timeout);
                }

                throw new OperationCanceledException(ct);
            }
            finally
            {
                _currentFifoTcs = null;
                _fifoLock.Release();
            }
        }
        else
        {
            var cid = _codec.ExtractCorrelationId(request) ?? Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<TMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[cid] = tcs;

            try
            {
                var cmd = new OutboundCommand(request, isUrgent: false);
                try
                {
                    await _outboundNormalQueue.Writer.WriteAsync(cmd, ct).ConfigureAwait(false);
                    await cmd.Completion.Task.ConfigureAwait(false);
                }
                catch (ChannelClosedException cce)
                {
                    throw new DeviceDisconnectedException("Hardware connection has been disconnected.", cce);
                }

                using var timeoutCts = new CancellationTokenSource(timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                var responseTask = tcs.Task;
                var completedTask = await Task.WhenAny(responseTask, Task.Delay(Timeout.Infinite, linkedCts.Token)).ConfigureAwait(false);

                if (completedTask == responseTask)
                {
                    var response = await responseTask.ConfigureAwait(false);
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Tx, TrafficKind.AperiodicCommand,
                        "REQUEST_RESP", ReadOnlyMemory<byte>.Empty, response?.ToString(), sw.GetElapsedTime(), LogLevel.Debug));

                    if (response is TResponse typedRes) return typedRes;
                    throw new InvalidCastException($"Expected {typeof(TResponse).Name}, received {response?.GetType().Name}");
                }

                _observer?.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Tx, TrafficKind.SpontaneousAlarm,
                    "DEVICE_TIMEOUT", ReadOnlyMemory<byte>.Empty,
                    $"Command '{request}' timed out after {timeout.TotalMilliseconds}ms.", sw.GetElapsedTime(), LogLevel.Warning));
                throw new DeviceTimeoutException(request?.ToString() ?? "UnknownCommand", timeout);
            }
            finally
            {
                _pendingRequests.TryRemove(cid, out _);
            }
        }
    }

    public async ValueTask SendUrgentAsync(TMessage urgentMessage)
    {
        EnsureConnected();
        var cmd = new OutboundCommand(urgentMessage, isUrgent: true);
        _outboundUrgentQueue.Writer.TryWrite(cmd);
        await cmd.Completion.Task.ConfigureAwait(false);

        _observer?.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow, PacketDirection.Tx, TrafficKind.AperiodicCommand,
            "URGENT_OOB", ReadOnlyMemory<byte>.Empty, urgentMessage?.ToString(), TimeSpan.Zero, LogLevel.Critical));
    }

    private void OnConnectionClosed()
    {
        if (Interlocked.Exchange(ref _isConnected, 0) == 1)
        {
            var ex = new DeviceDisconnectedException("Hardware connection has been disconnected. (Fail-fast aborting all pending requests)");
            _currentFifoTcs?.TrySetException(ex);
            foreach (var kvp in _pendingRequests)
            {
                kvp.Value.TrySetException(ex);
            }
            _pendingRequests.Clear();
            _incomingStream.Writer.TryComplete(ex);
            _outboundNormalQueue.Writer.TryComplete(ex);
            _outboundUrgentQueue.Writer.TryComplete(ex);
        }
    }

    private void EnsureConnected()
    {
        if (Volatile.Read(ref _isConnected) == 0 || _context == null)
        {
            throw new DeviceDisconnectedException("Connection is not open. Call StartAsync() first.");
        }
    }

    public async ValueTask StopAsync()
    {
        _sessionCts.Cancel();
        OnConnectionClosed();

        var tasksToWait = new List<Task>();
        if (_readLoopTask != null) tasksToWait.Add(_readLoopTask);
        if (_outboundPumpTask != null) tasksToWait.Add(_outboundPumpTask);
        if (_dispatchLoopTask != null) tasksToWait.Add(_dispatchLoopTask);
        if (_heartbeatTask != null) tasksToWait.Add(_heartbeatTask);

        if (tasksToWait.Count > 0)
        {
            var joinAllTask = Task.WhenAll(tasksToWait);
            var timeoutTask = Task.Delay(2000);
            await Task.WhenAny(joinAllTask, timeoutTask).ConfigureAwait(false);
        }

        if (_context != null)
        {
            await _context.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _fifoLock.Dispose();
        _sessionCts.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class OutboundCommand
    {
        public TMessage Message { get; }
        public bool IsUrgent { get; }
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public OutboundCommand(TMessage message, bool isUrgent)
        {
            Message = message;
            IsUrgent = isUrgent;
        }
    }
}
