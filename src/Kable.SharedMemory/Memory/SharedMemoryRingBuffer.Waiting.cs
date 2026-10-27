namespace Kable.SharedMemory.Memory;

using System;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class SharedMemoryRingBuffer
{
    private int _dataWaiter;
    private int _spaceWaiter;

    /// <summary>Waits for data or closure. Only one data waiter per endpoint is supported.</summary>
    public bool WaitForData(int timeoutMs, CancellationToken ct = default)
        => WaitForCondition(false, timeoutMs, ct);

    /// <summary>Waits for space or closure. Only one space waiter per endpoint is supported.</summary>
    public bool WaitForSpace(int timeoutMs, CancellationToken ct = default)
        => WaitForCondition(true, timeoutMs, ct);

    /// <summary>Returns readiness/closure, false on timeout, or cancellation. Does not occupy a worker while idle.</summary>
    public ValueTask<bool> WaitForDataAsync(int timeoutMs = Timeout.Infinite, CancellationToken ct = default)
        => WaitForConditionAsync(false, timeoutMs, ct);

    /// <summary>Returns readiness/closure, false on timeout, or cancellation. Does not occupy a worker while full.</summary>
    public ValueTask<bool> WaitForSpaceAsync(int timeoutMs = Timeout.Infinite, CancellationToken ct = default)
        => WaitForConditionAsync(true, timeoutMs, ct);

    private ValueTask<bool> WaitForConditionAsync(bool space, int timeoutMs, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMs, Timeout.Infinite);
        ct.ThrowIfCancellationRequested();
        if (!TryEnter()) return new ValueTask<bool>(true);
        bool transferred = false;
        try
        {
            if (IsReady(space)) return new ValueTask<bool>(true);
            if (timeoutMs == 0) return new ValueTask<bool>(false);
            EnterWait(space);
            transferred = true;
            return new ValueTask<bool>(WaitRegisteredAsync(space, timeoutMs, ct));
        }
        finally { if (!transferred) Exit(); }
    }

    private async Task<bool> WaitRegisteredAsync(bool space, int timeoutMs, CancellationToken ct)
    {
        long started = Environment.TickCount64;
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (IsReady(space)) return true;
                int remaining = Remaining(timeoutMs, started);
                if (remaining == 0) return IsReady(space);
                var handle = space ? _spaceAvailableEvent : _dataAvailableEvent;
                bool signaled = await RegisteredEventWait.WaitAsync(handle, remaining, ct).ConfigureAwait(false);
                if (!signaled)
                {
                    ct.ThrowIfCancellationRequested();
                    return IsReady(space);
                }
            }
        }
        finally
        {
            ExitWait(space);
            Exit();
        }
    }

    // Compatibility API; connection pumps use the asynchronous methods exclusively.
    private bool WaitForCondition(bool space, int timeoutMs, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMs, Timeout.Infinite);
        ct.ThrowIfCancellationRequested();
        if (!TryEnter()) return true;
        bool waiting = false;
        try
        {
            if (IsReady(space)) return true;
            if (timeoutMs == 0) return false;
            EnterWait(space);
            waiting = true;
            var handle = space ? _spaceAvailableEvent : _dataAvailableEvent;
            using var cancellation = ct.UnsafeRegister(static state => ((EventWaitHandle)state!).Set(), handle);
            long started = Environment.TickCount64;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (IsReady(space)) return true;
                int remaining = Remaining(timeoutMs, started);
                if (remaining == 0) return IsReady(space);
                if (!handle.WaitOne(remaining))
                {
                    ct.ThrowIfCancellationRequested();
                    return IsReady(space);
                }
            }
        }
        finally
        {
            if (waiting) ExitWait(space);
            Exit();
        }
    }

    private static int Remaining(int timeoutMs, long started)
        => timeoutMs == Timeout.Infinite ? Timeout.Infinite
            : (int)Math.Max(0, timeoutMs - (Environment.TickCount64 - started));

    private void EnterWait(bool space)
    {
        ref int slot = ref (space ? ref _spaceWaiter : ref _dataWaiter);
        if (Interlocked.CompareExchange(ref slot, 1, 0) != 0)
            throw new InvalidOperationException("An SPSC endpoint supports only one pending waiter per direction.");
    }

    private void ExitWait(bool space)
    {
        if (space) Volatile.Write(ref _spaceWaiter, 0);
        else Volatile.Write(ref _dataWaiter, 0);
    }
}
