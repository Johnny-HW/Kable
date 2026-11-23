namespace Kable.Core.Security.Guards;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core.Security;

/// <summary>
/// 에이전트/장비의 중복 실행 방지 및 Busy 락을 제어하는 고성능 단일 비행(Single-Flight) 동시성 가드입니다.
/// </summary>
public sealed class SingleFlightConcurrencyGuard : IConcurrencyGuard
{
    private readonly ConcurrencyOptions _options;
    private readonly object _tableSync = new();
    private readonly Dictionary<string, ResourceLockState> _locks = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public SingleFlightConcurrencyGuard(ConcurrencyOptions? options = null)
    {
        _options = options ?? new ConcurrencyOptions();
    }

    public bool IsBusy(string resourceKey)
    {
        if (string.IsNullOrEmpty(resourceKey)) return false;
        lock (_tableSync)
        {
            return _locks.TryGetValue(resourceKey, out var state) && state.IsActive;
        }
    }

    public async ValueTask<IAsyncDisposable?> TryAcquireAsync(string resourceKey, CancellationToken ct = default)
    {
        if (!_options.PreventDuplicateExecution)
        {
            return EmptyReleaser.Instance;
        }

        if (string.IsNullOrEmpty(resourceKey))
        {
            throw new ArgumentException("Resource key cannot be null or empty.", nameof(resourceKey));
        }

        ResourceLockState? state;
        lock (_tableSync)
        {
            if (_disposed) return EmptyReleaser.Instance;
            if (!_locks.TryGetValue(resourceKey, out state))
            {
                state = new ResourceLockState(resourceKey);
                _locks[resourceKey] = state;
            }
            state.RefCount++;
        }

        bool ownershipTransferred = false;
        try
        {
            switch (_options.Mode)
            {
                case BusyHandlingMode.RejectImmediately:
                    if (Interlocked.CompareExchange(ref state.ActiveCount, 1, 0) == 0)
                    {
                        ownershipTransferred = true;
                        return new ImmediateReleaser(this, state);
                    }
                    return null;

                case BusyHandlingMode.PreemptCurrent:
                    await state.Semaphore.WaitAsync(ct).ConfigureAwait(false);
                    ownershipTransferred = true;
                    return new SemaphoreReleaser(this, state);

                case BusyHandlingMode.EnqueueFifo:
                default:
                    if (Volatile.Read(ref state.WaitQueueCount) >= _options.MaxQueueCapacity)
                    {
                        return null;
                    }

                    Interlocked.Increment(ref state.WaitQueueCount);
                    try
                    {
                        var entered = await state.Semaphore.WaitAsync(_options.LockAcquisitionTimeout, ct).ConfigureAwait(false);
                        if (!entered)
                        {
                            return null;
                        }
                        ownershipTransferred = true;
                        return new SemaphoreReleaser(this, state);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref state.WaitQueueCount);
                    }
            }
        }
        finally
        {
            if (!ownershipTransferred)
            {
                ReleaseReference(state);
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
        ReleaseReference(state);
    }

    private void ReleaseSemaphore(ResourceLockState state)
    {
        state.Semaphore.Release();
        ReleaseReference(state);
    }

    private void ReleaseReference(ResourceLockState state)
    {
        bool shouldDispose = false;
        lock (_tableSync)
        {
            state.RefCount--;
            if (state.RefCount <= 0)
            {
                if (_locks.TryGetValue(state.ResourceKey, out var current) && ReferenceEquals(current, state))
                {
                    _locks.Remove(state.ResourceKey);
                }
                shouldDispose = true;
            }
        }

        if (shouldDispose)
        {
            state.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        List<ResourceLockState>? idleToDispose = null;
        lock (_tableSync)
        {
            if (_disposed) return default;
            _disposed = true;
            foreach (var kvp in _locks)
            {
                if (kvp.Value.RefCount <= 0)
                {
                    idleToDispose ??= new List<ResourceLockState>();
                    idleToDispose.Add(kvp.Value);
                }
            }
            _locks.Clear();
        }

        if (idleToDispose != null)
        {
            for (int i = 0; i < idleToDispose.Count; i++)
            {
                idleToDispose[i].Dispose();
            }
        }

        return default;
    }

    private sealed class ResourceLockState : IDisposable
    {
        public readonly string ResourceKey;
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int ActiveCount;
        public int WaitQueueCount;
        public int RefCount;

        public bool IsActive => Volatile.Read(ref ActiveCount) > 0 || Semaphore.CurrentCount == 0;

        public ResourceLockState(string resourceKey)
        {
            ResourceKey = resourceKey;
        }

        public void Dispose() => Semaphore.Dispose();
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
