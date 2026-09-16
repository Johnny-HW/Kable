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

public sealed class KableSession<TMessage> : IDeviceSession<TMessage>
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
        if (Interlocked.CompareExchange(ref _isConnected, 1, 0) != 0) return;

        _context = await _connectionFactory.ConnectAsync(ct).ConfigureAwait(false);
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
        await _outboundNormalQueue.Writer.WriteAsync(cmd, ct).ConfigureAwait(false);
        await cmd.Completion.Task.ConfigureAwait(false);

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
                await _outboundNormalQueue.Writer.WriteAsync(cmd, ct).ConfigureAwait(false);
                await cmd.Completion.Task.ConfigureAwait(false);

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
                await _outboundNormalQueue.Writer.WriteAsync(cmd, ct).ConfigureAwait(false);
                await cmd.Completion.Task.ConfigureAwait(false);

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

    /// <summary>
    /// P0: Single Outbound Writer Pump Loop.
    /// Serializes access to PipeWriter, prioritizes urgent messages, and batches flush calls.
    /// </summary>
    private async Task OutboundPumpLoopAsync()
    {
        var output = _context!.Output;
        var token = _sessionCts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                OutboundCommand cmd;

                // 1. Check Urgent Queue first
                if (!_outboundUrgentQueue.Reader.TryRead(out cmd!))
                {
                    // If no urgent, wait for whichever comes first
                    var urgentWait = _outboundUrgentQueue.Reader.WaitToReadAsync(token).AsTask();
                    var normalWait = _outboundNormalQueue.Reader.WaitToReadAsync(token).AsTask();

                    var readyTask = await Task.WhenAny(urgentWait, normalWait).ConfigureAwait(false);
                    if (!await readyTask.ConfigureAwait(false))
                    {
                        break;
                    }

                    if (!_outboundUrgentQueue.Reader.TryRead(out cmd!) &&
                        !_outboundNormalQueue.Reader.TryRead(out cmd!))
                    {
                        continue;
                    }
                }

                // 2. Encode primary message
                var commandsToComplete = new List<OutboundCommand>(8) { cmd };
                _codec.Encode(cmd.Message, output);

                // 3. Batching: drain any currently pending messages before triggering syscall flush
                while (_outboundUrgentQueue.Reader.TryRead(out var queuedUrgent))
                {
                    commandsToComplete.Add(queuedUrgent);
                    _codec.Encode(queuedUrgent.Message, output);
                }

                while (commandsToComplete.Count < 32 && _outboundNormalQueue.Reader.TryRead(out var queuedNormal))
                {
                    commandsToComplete.Add(queuedNormal);
                    _codec.Encode(queuedNormal.Message, output);
                }

                // 4. Single consolidated FlushAsync
                try
                {
                    var flushResult = await output.FlushAsync(token).ConfigureAwait(false);
                    foreach (var c in commandsToComplete)
                    {
                        c.Completion.TrySetResult(true);
                    }

                    if (flushResult.IsCompleted || flushResult.IsCanceled)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Exception translatedEx = ex;
                    if (ex is System.IO.IOException or System.Net.Sockets.SocketException)
                    {
                        translatedEx = new DeviceDisconnectedException("Hardware connection was lost during data transmission.", ex);
                    }

                    foreach (var c in commandsToComplete)
                    {
                        c.Completion.TrySetException(translatedEx);
                    }
                    throw;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cooperative exit
        }
        catch (Exception ex)
        {
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Tx, TrafficKind.SpontaneousAlarm,
                "IO_FLUSH_ERROR", ReadOnlyMemory<byte>.Empty, ex.Message, TimeSpan.Zero, LogLevel.Error));
            OnConnectionClosed();
        }
    }

    private async Task ReadLoopAsync()
    {
        var input = _context!.Input;
        var token = _sessionCts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var result = await input.ReadAsync(token).ConfigureAwait(false);
                var buffer = result.Buffer;

                while (_codec.TryDecode(ref buffer, out var message))
                {
                    Volatile.Write(ref _lastInboundTicks, DateTime.UtcNow.Ticks);
                    await _dispatchQueue.Writer.WriteAsync(message, token).ConfigureAwait(false);
                }

                input.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted || result.IsCanceled) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cooperative cancellation
        }
        catch (Exception ex)
        {
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                "READ_LOOP_FAULT", ReadOnlyMemory<byte>.Empty,
                $"{ex.GetType().Name}: {ex.Message}", TimeSpan.Zero, LogLevel.Error));
            OnConnectionClosed();
        }
        finally
        {
            _dispatchQueue.Writer.TryComplete();
            OnConnectionClosed();
        }
    }

    private async Task DispatchLoopAsync()
    {
        var reader = _dispatchQueue.Reader;

        try
        {
            while (await reader.WaitToReadAsync(_sessionCts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var message))
                {
                    try
                    {
                        DispatchMessage(message);
                    }
                    catch (Exception ex)
                    {
                        _observer?.OnPacketTrace(new PacketTraceRecord(
                            DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                            "DISPATCH_MESSAGE_FAULT", ReadOnlyMemory<byte>.Empty,
                            $"{ex.GetType().Name}: {ex.Message}", TimeSpan.Zero, LogLevel.Error));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cooperative cancellation
        }
        catch (Exception ex)
        {
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                "DISPATCH_LOOP_FAULT", ReadOnlyMemory<byte>.Empty,
                $"{ex.GetType().Name}: {ex.Message}", TimeSpan.Zero, LogLevel.Error));
        }
        finally
        {
            while (reader.TryRead(out var residualMessage))
            {
                try
                {
                    DispatchMessage(residualMessage);
                }
                catch (Exception ex)
                {
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                        "DRAIN_DISPATCH_FAULT", ReadOnlyMemory<byte>.Empty,
                        $"{ex.GetType().Name}: {ex.Message}", TimeSpan.Zero, LogLevel.Warning));
                }
            }
        }
    }

    private void DispatchMessage(TMessage message)
    {
        Volatile.Write(ref _lastInboundTicks, DateTime.UtcNow.Ticks);

        if (_heartbeatOptions?.IsPongResponse != null && _heartbeatOptions.IsPongResponse(message))
        {
            return;
        }

        // Autonomous / Unsolicited stream check
        if (_codec.IsAutonomousMessage(message))
        {
            _incomingStream.Writer.TryWrite(message);
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                "STREAM", ReadOnlyMemory<byte>.Empty, message?.ToString(), TimeSpan.Zero));
            return;
        }

        // P1: Correlation Matching First (Lowest Tail Latency)
        if (_codec.SupportsCorrelationId)
        {
            var cid = _codec.ExtractCorrelationId(message);
            if (cid != null && _pendingRequests.TryRemove(cid, out var tcs))
            {
                tcs.TrySetResult(message);
                return;
            }
        }
        else
        {
            if (_currentFifoTcs != null && !_currentFifoTcs.Task.IsCompleted)
            {
                _currentFifoTcs.TrySetResult(message);
                return;
            }
        }

        // Fallback to incoming stream
        _incomingStream.Writer.TryWrite(message);
        _observer?.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
            "STREAM", ReadOnlyMemory<byte>.Empty, message?.ToString(), TimeSpan.Zero));
    }

    private async Task HeartbeatLoopAsync()
    {
        if (_heartbeatOptions == null) return;
        var checkInterval = TimeSpan.FromMilliseconds(Math.Max(100, _heartbeatOptions.Interval.TotalMilliseconds / 2));

        try
        {
            while (!_sessionCts.Token.IsCancellationRequested)
            {
                await Task.Delay(checkInterval, _sessionCts.Token).ConfigureAwait(false);

                var lastTicks = Volatile.Read(ref _lastInboundTicks);
                var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - lastTicks);

                if (elapsed > _heartbeatOptions.Timeout)
                {
                    _observer?.OnPacketTrace(new PacketTraceRecord(
                        DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                        "HEARTBEAT_TIMEOUT", ReadOnlyMemory<byte>.Empty,
                        $"Heartbeat timeout. No inbound traffic for {elapsed.TotalMilliseconds:F0}ms (limit: {_heartbeatOptions.Timeout.TotalMilliseconds:F0}ms).",
                        elapsed, LogLevel.Critical));

                    OnConnectionClosed();
                    break;
                }

                // Send Ping via Outbound queue
                try
                {
                    var pingMsg = _heartbeatOptions.PingFactory();
                    var cmd = new OutboundCommand(pingMsg, isUrgent: false);
                    if (_outboundNormalQueue.Writer.TryWrite(cmd))
                    {
                        await cmd.Completion.Task.ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
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
