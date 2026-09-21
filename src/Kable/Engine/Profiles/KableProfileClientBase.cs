namespace Kable.Engine.Profiles;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kable.Observability;
using Kable.Simple;

/// <summary>
/// 상시/수시 통신 클라이언트의 공통 뼈대 (주기적 폴링, 0-드리프트 타이머, 캐시 타임스탬프, 안전한 약한 참조 구독, 수명 관리)
/// </summary>
internal abstract class KableProfileClientBase<TPeriodic> : IAsyncDisposable, IDisposable
    where TPeriodic : notnull
{
    private readonly IKableSimpleClient _client;
    private readonly KableProfileConfigBase _configBase;
    private readonly ICommObserver? _observer;
    private readonly ConcurrentDictionary<TPeriodic, (string Value, DateTime LastUpdatedUtc)> _latestCache;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _pollingTasks = new();
    private readonly List<IWeakSubscription<TPeriodic>> _weakSubscriptions = new();
    private readonly object _subLock = new();
    private bool _disposed;

    public bool IsConnected => _client.IsConnected;
    public event Action<TPeriodic, string>? PeriodicDataReceived;
    public event Action<Exception?>? Disconnected;

    protected IKableSimpleClient Client => _client;
    protected KableProfileConfigBase ConfigBase => _configBase;
    protected ICommObserver? Observer => _observer;

    protected KableProfileClientBase(
        IKableSimpleClient client,
        KableProfileConfigBase configBase,
        ICommObserver? observer,
        IEqualityComparer<TPeriodic>? keyComparer = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _configBase = configBase ?? throw new ArgumentNullException(nameof(configBase));
        _observer = observer;
        _latestCache = keyComparer != null
            ? new ConcurrentDictionary<TPeriodic, (string Value, DateTime LastUpdatedUtc)>(keyComparer)
            : new ConcurrentDictionary<TPeriodic, (string Value, DateTime LastUpdatedUtc)>();

        _client.Disconnected += OnClientDisconnected;
    }

    private void OnClientDisconnected(Exception? reason)
    {
        Disconnected?.Invoke(reason);
    }

    /// <summary>
    /// 백그라운드 주기 폴링 태스크 등록
    /// </summary>
    protected void StartPolling(TPeriodic key, string rawCommand, TimeSpan interval)
    {
        var safeInterval = interval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(200) : interval;
        _pollingTasks.Add(Task.Run(() => PollingLoopAsync(key, rawCommand, safeInterval, _cts.Token)));
    }

    private async Task PollingLoopAsync(TPeriodic key, string rawCommand, TimeSpan interval, CancellationToken ct)
    {
#if NET6_0_OR_GREATER
        using var timer = new PeriodicTimer(interval);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(ct).ConfigureAwait(false)) break;

                if (!_client.IsConnected) continue;

                var queryTimeout = _configBase.DefaultCommandTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(2) : _configBase.DefaultCommandTimeout;
                var response = await _client.QueryAsync(rawCommand, queryTimeout, ct).ConfigureAwait(false);
                _latestCache[key] = (response, DateTime.UtcNow);
#else
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct).ConfigureAwait(false);

                if (!_client.IsConnected) continue;

                var queryTimeout = _configBase.DefaultCommandTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(2) : _configBase.DefaultCommandTimeout;
                var response = await _client.QueryAsync(rawCommand, queryTimeout, ct).ConfigureAwait(false);
                _latestCache[key] = (response, DateTime.UtcNow);
#endif

                _observer?.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Rx, TrafficKind.PeriodicTelemetry,
                    rawCommand, ReadOnlyMemory<byte>.Empty, response, TimeSpan.Zero));

                PeriodicDataReceived?.Invoke(key, response);
                NotifyWeakSubscribers(key, response);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _observer?.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                    rawCommand, ReadOnlyMemory<byte>.Empty, ex.Message, TimeSpan.Zero, LogLevel.Warning));
            }
        }
    }

    private void NotifyWeakSubscribers(TPeriodic key, string response)
    {
        IWeakSubscription<TPeriodic>[] snapshot;
        lock (_subLock)
        {
            if (_weakSubscriptions.Count == 0) return;

            // 1. 수거된 약한 참조 정리
            for (int i = _weakSubscriptions.Count - 1; i >= 0; i--)
            {
                if (!_weakSubscriptions[i].IsTargetAlive)
                {
                    _weakSubscriptions.RemoveAt(i);
                }
            }

            if (_weakSubscriptions.Count == 0) return;
            snapshot = _weakSubscriptions.ToArray();
        }

        // 2. 락 외부에서 안전하게 핸들러 디스패치 (데드락 방지)
        for (int i = 0; i < snapshot.Length; i++)
        {
            snapshot[i].Invoke(key, response);
        }
    }

    public IDisposable SubscribeWeak<TTarget>(TTarget target, Action<TTarget, TPeriodic, string> handler) where TTarget : class
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var sub = new WeakSubscriptionImpl<TTarget, TPeriodic>(target, handler, RemoveWeakSubscription);
        lock (_subLock)
        {
            _weakSubscriptions.Add(sub);
        }
        return sub;
    }

    public IDisposable SubscribeWeak<TTarget, TState>(TTarget target, TState state, Action<TTarget, TState, TPeriodic, string> handler) where TTarget : class
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var sub = new WeakSubscriptionWithStateImpl<TTarget, TState, TPeriodic>(target, state, handler, RemoveWeakSubscription);
        lock (_subLock)
        {
            _weakSubscriptions.Add(sub);
        }
        return sub;
    }

    private void RemoveWeakSubscription(IWeakSubscription<TPeriodic> sub)
    {
        lock (_subLock)
        {
            _weakSubscriptions.Remove(sub);
        }
    }

    public string? GetLatest(TPeriodic command)
    {
        return _latestCache.TryGetValue(command, out var item) ? item.Value : null;
    }

    public (string? Value, DateTime LastUpdatedUtc) GetLatestWithTimestamp(TPeriodic command)
    {
        return _latestCache.TryGetValue(command, out var item)
            ? (item.Value, item.LastUpdatedUtc)
            : (null, DateTime.MinValue);
    }

    public bool TryGetFresh(TPeriodic command, TimeSpan maxAge, out string? value)
    {
        if (_latestCache.TryGetValue(command, out var item))
        {
            if (DateTime.UtcNow - item.LastUpdatedUtc <= maxAge)
            {
                value = item.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// 세션 재시작 없이 기본 명령 타임아웃을 동적으로 핫 리로드(Hot-Reload)합니다.
    /// </summary>
    public void UpdateDefaultCommandTimeout(TimeSpan newTimeout)
    {
        ThrowIfDisposed();
        if (newTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(newTimeout), "Timeout must be positive.");
        _configBase.DefaultCommandTimeout = newTimeout;
    }

    protected void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Disconnected -= OnClientDisconnected;
        _cts.Cancel();
#pragma warning disable CS0618
        _client.Dispose();
#pragma warning restore CS0618
        _cts.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Disconnected -= OnClientDisconnected;
        _cts.Cancel();
        await _client.DisposeAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(_pollingTasks).ConfigureAwait(false);
        }
        catch
        {
            // ignore cancellation
        }
        _cts.Dispose();
    }
}
