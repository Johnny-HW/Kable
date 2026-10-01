namespace Kable.Simple.Reconnecting;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Exceptions;

/// <summary>
/// Opt-in recovery using fresh clients. Failed commands are never retried or queued for a future connection.
/// Events run on background threads. Use await using; connection factories must honor cancellation.
/// </summary>
public sealed class ReconnectingKableClient : IAsyncDisposable
{
    private readonly Func<CancellationToken, ValueTask<IKableSimpleClient>> _factory;
    private readonly ReconnectOptions _options;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _disposeGate = new();
    private IKableSimpleClient? _client;
    private Task? _disposeTask;
    private int _disposed;
    private Exception? _lastError;

    public bool IsConnected => Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _client)?.IsConnected == true;
    public Exception? LastError => Volatile.Read(ref _lastError);
    /// <summary>Completes on disposal or exhausted retries. LastError describes recovery failure.</summary>
    public Task RecoveryTask { get; private set; } = Task.CompletedTask;
    public event Action<string>? LineReceived;
    public event Action<Exception>? ErrorOccurred;
    public event Action<Exception?>? Disconnected;
    public event Action? Reconnected;

    private ReconnectingKableClient(Func<CancellationToken, ValueTask<IKableSimpleClient>> factory,
        ReconnectOptions options)
    {
        _factory = factory;
        _options = options;
    }

    /// <summary>Initial connection errors are returned immediately; subsequent outages use finite retries.</summary>
    public static async ValueTask<ReconnectingKableClient> OpenAsync(
        Func<CancellationToken, ValueTask<IKableSimpleClient>> factory,
        ReconnectOptions? options = null, CancellationToken ct = default)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        var settings = options ?? new ReconnectOptions();
        settings.Validate();
        var wrapper = new ReconnectingKableClient(factory, settings);
        try
        {
            wrapper._client = await wrapper.CreateClientAsync(ct).ConfigureAwait(false);
            wrapper.Attach(wrapper._client);
            wrapper.RecoveryTask = Task.Run(wrapper.RecoverAsync);
            return wrapper;
        }
        catch
        {
            await wrapper.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask<string> QueryAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
        => GetConnectedClient().QueryAsync(command, timeout, ct);

    public ValueTask SendLineAsync(string command, CancellationToken ct = default)
        => GetConnectedClient().SendLineAsync(command, ct);

    private IKableSimpleClient GetConnectedClient()
    {
        if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(ReconnectingKableClient));
        var client = Volatile.Read(ref _client);
        if (client?.IsConnected != true)
            throw new DeviceDisconnectedException("Connection is recovering or retries are exhausted. Command was not queued.");
        return client;
    }

    private async ValueTask<IKableSimpleClient> CreateClientAsync(CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        deadline.CancelAfter(_options.ConnectTimeout);
        var client = await _factory(deadline.Token).ConfigureAwait(false);
        if (client == null) throw new InvalidOperationException("Factory returned null.");
        if (deadline.IsCancellationRequested || !client.IsConnected)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            throw new DeviceDisconnectedException("Factory must return a fresh, connected client.");
        }
        return client;
    }

    private async Task RecoverAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                await Task.Delay(_options.RetryDelay, _lifetime.Token).ConfigureAwait(false);
                if (Volatile.Read(ref _client)?.IsConnected == true) continue;
                await ReleaseClientAsync().ConfigureAwait(false);
                bool restored = false;
                for (int attempt = 0; attempt < _options.MaxAttempts; attempt++)
                {
                    if (attempt > 0)
                        await Task.Delay(_options.RetryDelay, _lifetime.Token).ConfigureAwait(false);
                    try
                    {
                        var next = await CreateClientAsync(_lifetime.Token).ConfigureAwait(false);
                        Attach(next);
                        Volatile.Write(ref _client, next);
                        Volatile.Write(ref _lastError, null);
                        InvokeHandlers(Reconnected);
                        restored = true;
                        break;
                    }
                    catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
                    catch (Exception error) { ReportError(error); }
                }
                if (!restored) return;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) { ReportError(error); }
    }

    private void Attach(IKableSimpleClient client)
    {
        client.LineReceived += ForwardLine;
        client.ErrorOccurred += ReportError;
        client.Disconnected += ForwardDisconnect;
    }

    private async ValueTask ReleaseClientAsync()
    {
        var client = Interlocked.Exchange(ref _client, null);
        if (client == null) return;
        client.LineReceived -= ForwardLine;
        client.ErrorOccurred -= ReportError;
        client.Disconnected -= ForwardDisconnect;
        await client.DisposeAsync().ConfigureAwait(false);
    }

    private void ForwardLine(string line) => InvokeHandlers(LineReceived, line);
    private void ForwardDisconnect(Exception? reason) => InvokeHandlers(Disconnected, reason);

    private void InvokeHandlers<T>(Action<T>? handlers, T value)
    {
        if (handlers == null) return;
        foreach (Action<T> handler in handlers.GetInvocationList())
        {
            try { handler(value); }
            catch (Exception error) { ReportError(error); }
        }
    }

    private void InvokeHandlers(Action? handlers)
    {
        if (handlers == null) return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception error) { ReportError(error); }
        }
    }

    private void ReportError(Exception error)
    {
        Volatile.Write(ref _lastError, error);
        var handlers = ErrorOccurred;
        if (handlers == null) return;
        foreach (Action<Exception> handler in handlers.GetInvocationList())
        {
            try { handler(error); }
            catch (Exception handlerError) { Volatile.Write(ref _lastError, handlerError); }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate) return new ValueTask(_disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        _lifetime.Cancel();
        try
        {
            await RecoveryTask.ConfigureAwait(false);
            await ReleaseClientAsync().ConfigureAwait(false);
        }
        finally { _lifetime.Dispose(); }
    }
}
