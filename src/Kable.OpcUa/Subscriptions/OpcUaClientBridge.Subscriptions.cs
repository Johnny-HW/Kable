namespace Kable.OpcUa;

using Kable.OpcUa.Subscriptions;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

public sealed partial class OpcUaClientBridge
{
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly List<OpcUaValueSubscription> _subscriptions = [];
    private readonly object _lifetime = new();
    private Task? _recovery, _disposal;
    private int _needsReconnect;
    public Exception? LastRecoveryError { get; private set; }

    public async Task<OpcUaValueSubscription> SubscribeAsync(IEnumerable<string> nodes, OpcUaSubscriptionOptions? options = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        var handle = new OpcUaValueSubscription(nodes.ToArray(), options ?? new(), RemoveSubscriptionAsync);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
        await _sessionGate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            await ConnectAndRestoreAsync(linked.Token).ConfigureAwait(false);
            await handle.AttachAsync(_session!, linked.Token).ConfigureAwait(false);
            _subscriptions.Add(handle);
            _recovery ??= RecoveryLoopAsync(_shutdown.Token);
            return handle;
        }
        catch
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await handle.CloseAsync(timeout.Token).ConfigureAwait(false);
            }
            finally { handle.Complete(); }
            throw;
        }
        finally { _sessionGate.Release(); }
    }

    private async Task RemoveSubscriptionAsync(OpcUaValueSubscription handle)
    {
        await _sessionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _subscriptions.Remove(handle);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await handle.CloseAsync(timeout.Token).ConfigureAwait(false);
        }
        finally { _sessionGate.Release(); }
    }

    private async Task ConnectAndRestoreAsync(CancellationToken ct)
    {
        if (IsConnected) return;
        _session?.Dispose();
        _session = null;
        DisposeIdentity();
        _configuration ??= await CreateSecurityConfigurationAsync(_security).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
#pragma warning disable CS0618
        var application = new ApplicationInstance { ApplicationConfiguration = _configuration };
        await application.CheckApplicationInstanceCertificatesAsync(true, null, ct).ConfigureAwait(false);
        var endpoint = await SelectSecureEndpointAsync(_configuration, ct).ConfigureAwait(false);
        var configured = new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create(_configuration));
        _identity = CreateUserIdentity();
        try
        {
            _session = await Session.Create(_configuration, configured, false, true, "KableClientSession", _sessionTimeoutMs,
                _identity, null, ct).ConfigureAwait(false);
        }
        catch { DisposeIdentity(); throw; }
#pragma warning restore CS0618
        _session.KeepAliveInterval = 500;
        _session.KeepAlive += OnKeepAlive;
        try
        {
            foreach (var subscription in _subscriptions) await subscription.AttachAsync(_session, ct).ConfigureAwait(false);
            Volatile.Write(ref _needsReconnect, 0);
            LastRecoveryError = null;
        }
        catch { Volatile.Write(ref _needsReconnect, 1); throw; }
    }

    private void OnKeepAlive(ISession session, KeepAliveEventArgs args)
    {
        if (ReferenceEquals(session, _session) && ServiceResult.IsBad(args.Status)) Volatile.Write(ref _needsReconnect, 1);
    }
    private async Task RecoveryLoopAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(500, ct).ConfigureAwait(false);
                await _sessionGate.WaitAsync(ct).ConfigureAwait(false);
                try { if (_subscriptions.Count > 0) await ConnectAndRestoreAsync(ct).ConfigureAwait(false); }
                catch (Exception error) when (!ct.IsCancellationRequested) { LastRecoveryError = error; }
                finally { _sessionGate.Release(); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* Normal shutdown. */ }
    }
    private async Task DisposeCoreAsync()
    {
        Interlocked.Exchange(ref _isDisposed, 1);
        var errors = new List<Exception>();
        try { _shutdown.Cancel(); } catch (Exception error) { errors.Add(error); }
        try { if (_recovery != null) await _recovery.ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        await _sessionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var handle in _subscriptions)
            {
                try { handle.Complete(); } catch (Exception error) { errors.Add(error); }
            }
            _subscriptions.Clear();
            var session = _session;
            _session = null;
            if (session != null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await session.CloseAsync(timeout.Token).ConfigureAwait(false); }
                catch (Exception error) { errors.Add(error); }
                try { session.Dispose(); } catch (Exception error) { errors.Add(error); }
            }
        }
        finally
        {
            try { DisposeIdentity(); }
            finally { _shutdown.Dispose(); _sessionGate.Release(); }
        }
        if (errors.Count > 0) throw new AggregateException("OPC UA shutdown failed after resource cleanup.", errors);
    }
}
