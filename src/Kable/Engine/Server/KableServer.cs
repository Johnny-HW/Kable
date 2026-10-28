namespace Kable.Engine;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;

/// <summary>Owns a listener and independent bounded sessions for incoming connections.</summary>
public sealed partial class KableServer<TMessage> : IKableServer<TMessage>
{
    private readonly IConnectionListener _listener;
    private readonly Func<IProtocolCodec<TMessage>> _codecFactory;
    private readonly KableServerOptions _options;
    private readonly KableServerSessionRegistry<TMessage> _registry = new();
    private readonly CancellationTokenSource _serverCts = new();
    private readonly object _lifecycle = new();
    private Task? _acceptLoopTask;
    private Task? _stopTask;
    private Task? _disposeTask;
    private int _state; // 0 = created, 1 = running, 2 = stopping/stopped, 3 = disposed

    public bool IsRunning => Volatile.Read(ref _state) == 1;
    public int ActiveSessionCount => _registry.Count;
    public IReadOnlyCollection<IServerSession<TMessage>> ActiveSessions => _registry.Sessions;
    public event EventHandler<ServerSessionEventArgs<TMessage>>? ClientConnected;
    public event EventHandler<ServerSessionEventArgs<TMessage>>? ClientDisconnected;
    public event EventHandler<ServerMessageReceivedEventArgs<TMessage>>? MessageReceived;

    public KableServer(IConnectionListener listener, Func<IProtocolCodec<TMessage>> codecFactory,
        KableServerOptions? options = null)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _codecFactory = codecFactory ?? throw new ArgumentNullException(nameof(codecFactory));
        _options = (options ?? new KableServerOptions()).Snapshot();
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lifecycle)
        {
            if (_state == 3) throw new ObjectDisposedException(nameof(KableServer<TMessage>));
            if (_state != 0) throw new InvalidOperationException("Create a new server after stopping; server is already started or stopped.");
            Volatile.Write(ref _state, 1);
            _acceptLoopTask = Task.Run(() => RunAcceptLoopAsync(_serverCts.Token));
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync(TimeSpan timeout = default, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var duration = timeout == default ? _options.DefaultShutdownTimeout : timeout;
        KableServerOptions.ValidateTimeout(duration, nameof(timeout));
        Task cleanup;
        lock (_lifecycle)
        {
            if (_state == 3) cleanup = _stopTask ?? Task.CompletedTask;
            else
            {
                Volatile.Write(ref _state, 2);
                cleanup = _stopTask ??= Task.Run(() => StopCoreAsync(duration));
            }
        }
        await WaitForCallerAsync(cleanup, ct).ConfigureAwait(false);
    }

    private async Task StopCoreAsync(TimeSpan timeout)
    {
        _serverCts.Cancel();
        _listener.Stop();
        if (_acceptLoopTask != null) await _acceptLoopTask.ConfigureAwait(false);
        var sessions = _registry.Sessions;
        var completions = new List<Task>(sessions.Count);
        foreach (var item in sessions)
        {
            var session = (KableServerSession<TMessage>)item;
            session.CompleteSending();
            completions.Add(session.Completion);
        }
        var all = Task.WhenAll(completions);
        using (var deadline = new CancellationTokenSource())
        {
            var delay = Task.Delay(timeout, deadline.Token);
            if (await Task.WhenAny(all, delay).ConfigureAwait(false) == delay)
                foreach (var session in sessions) session.Disconnect("Shutdown drain deadline elapsed");
            deadline.Cancel();
        }
        await all.ConfigureAwait(false);
    }

    private static async Task WaitForCallerAsync(Task operation, CancellationToken ct)
    {
        if (!ct.CanBeCanceled) { await operation.ConfigureAwait(false); return; }
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (ct.Register(() => canceled.TrySetResult(true)))
        {
            if (await Task.WhenAny(operation, canceled.Task).ConfigureAwait(false) != operation)
                ct.ThrowIfCancellationRequested();
            await operation.ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifecycle)
        {
            if (_disposeTask != null) return new ValueTask(_disposeTask);
            Volatile.Write(ref _state, 3);
            _stopTask ??= Task.Run(() => StopCoreAsync(_options.DefaultShutdownTimeout));
            _disposeTask = DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        try { await _stopTask!.ConfigureAwait(false); }
        finally
        {
            try { await _listener.DisposeAsync().ConfigureAwait(false); }
            finally { _serverCts.Dispose(); }
        }
    }

    private void SafeInvoke<TArgs>(EventHandler<TArgs>? handler, TArgs args) where TArgs : EventArgs
    {
        if (handler == null) return;
        foreach (EventHandler<TArgs> subscriber in handler.GetInvocationList())
        {
            try { subscriber(this, args); }
            catch (Exception error) { Trace.TraceError("KableServer event handler failed: {0}", error); }
        }
    }
}
