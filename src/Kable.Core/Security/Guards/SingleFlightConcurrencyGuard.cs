namespace Kable.Core.Security.Guards;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core.Security;

/// <summary>
/// 에이전트/장비의 중복 실행 방지 및 Busy 락을 제어하는 고성능 단일 비행(Single-Flight) 동시성 가드입니다.
/// </summary>
public sealed class SingleFlightConcurrencyGuard : IConcurrencyGuard
{
    private readonly ConcurrencyOptions _options;
    private readonly ConcurrentDictionary<string, ResourceLockState> _locks = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public SingleFlightConcurrencyGuard(ConcurrencyOptions? options = null)
    {
        _options = options ?? new ConcurrencyOptions();
    }

    public bool IsBusy(string resourceKey)
    {
        if (string.IsNullOrEmpty(resourceKey)) return false;
        return _locks.TryGetValue(resourceKey, out var state) && state.IsActive;
    }

    public async ValueTask<IAsyncDisposable?> TryAcquireAsync(string resourceKey, CancellationToken ct = default)
    {
        if (_disposed || !_options.PreventDuplicateExecution)
        {
            return EmptyReleaser.Instance;
        }

        if (string.IsNullOrEmpty(resourceKey))
        {
            throw new ArgumentException("Resource key cannot be null or empty.", nameof(resourceKey));
        }

        var state = _locks.GetOrAdd(resourceKey, static key => new ResourceLockState(key));

        switch (_options.Mode)
        {
            case BusyHandlingMode.RejectImmediately:
                if (Interlocked.CompareExchange(ref state.ActiveCount, 1, 0) == 0)
                {
                    return new ImmediateReleaser(this, state);
                }
                return null;

            case BusyHandlingMode.PreemptCurrent:
                // 선행 작업 취소 신호 전달
                state.SignalCancelCurrent();
                await state.Semaphore.WaitAsync(ct).ConfigureAwait(false);
                state.ResetCancellation();
                return new SemaphoreReleaser(this, state);

            case BusyHandlingMode.EnqueueFifo:
            default:
                if (state.WaitQueueCount >= _options.MaxQueueCapacity)
                {
                    return null; // 대기열 용량 초과로 거부
                }

                Interlocked.Increment(ref state.WaitQueueCount);
                try
                {
                    var entered = await state.Semaphore.WaitAsync(_options.LockAcquisitionTimeout, ct).ConfigureAwait(false);
                    if (!entered)
                    {
                        return null; // 타임아웃
                    }
                    return new SemaphoreReleaser(this, state);
                }
                finally
                {
                    Interlocked.Decrement(ref state.WaitQueueCount);
                }
        }
    }

    public async ValueTask<IAsyncDisposable> AcquireAsync(string resourceKey, CancellationToken ct = default)
    {
        var handle = await TryAcquireAsync(resourceKey, ct).ConfigureAwait(false);
        if (handle == null)
        {
            throw new DeviceBusyException(resourceKey, $"Resource or device '{resourceKey}' is busy executing another operation.");
        }
        return handle;
    }

    private void ReleaseImmediate(ResourceLockState state)
    {
        Interlocked.Exchange(ref state.ActiveCount, 0);
    }

    private void ReleaseSemaphore(ResourceLockState state)
    {
        state.Semaphore.Release();
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            foreach (var kvp in _locks)
            {
                kvp.Value.Dispose();
            }
            _locks.Clear();
        }
        return default;
    }

    private sealed class ResourceLockState : IDisposable
    {
        public readonly string ResourceKey;
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int ActiveCount;
        public int WaitQueueCount;
        private CancellationTokenSource? _currentCts;

        public bool IsActive => ActiveCount > 0 || Semaphore.CurrentCount == 0;

        public ResourceLockState(string resourceKey)
        {
            ResourceKey = resourceKey;
        }

        public void SignalCancelCurrent()
        {
            var oldCts = Interlocked.Exchange(ref _currentCts, null);
            oldCts?.Cancel();
            oldCts?.Dispose();
        }

        public void ResetCancellation()
        {
            _currentCts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            SignalCancelCurrent();
            Semaphore.Dispose();
        }
    }

    private sealed class ImmediateReleaser : IAsyncDisposable
    {
        private SingleFlightConcurrencyGuard? _owner;
        private readonly ResourceLockState _state;

        public ImmediateReleaser(SingleFlightConcurrencyGuard owner, ResourceLockState state)
        {
            _owner = owner;
            _state = state;
        }

        public ValueTask DisposeAsync()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.ReleaseImmediate(_state);
            return default;
        }
    }

    private sealed class SemaphoreReleaser : IAsyncDisposable
    {
        private SingleFlightConcurrencyGuard? _owner;
        private readonly ResourceLockState _state;

        public SemaphoreReleaser(SingleFlightConcurrencyGuard owner, ResourceLockState state)
        {
            _owner = owner;
            _state = state;
        }

        public ValueTask DisposeAsync()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.ReleaseSemaphore(_state);
            return default;
        }
    }

    private sealed class EmptyReleaser : IAsyncDisposable
    {
        public static readonly EmptyReleaser Instance = new();
        public ValueTask DisposeAsync() => default;
    }
}
