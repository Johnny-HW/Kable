namespace Kable.SharedMemory.Waveform;

using System;
using System.Threading;

public unsafe sealed partial class SharedMemoryWaveformBuffer
{
    public bool WaitForData(int timeoutMs)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMs, Timeout.Infinite);
        EnterOrThrow();
        try
        {
            if (IsReadyForRead()) return true;
            if (timeoutMs == 0) return false;

            long started = Environment.TickCount64;
            while (true)
            {
                if (IsReadyForRead()) return true;
                int remaining = timeoutMs == Timeout.Infinite
                    ? Timeout.Infinite
                    : (int)Math.Max(0, timeoutMs - (Environment.TickCount64 - started));
                if (remaining == 0) return IsReadyForRead();
                if (!_dataEvent.WaitOne(remaining)) return IsReadyForRead();
            }
        }
        finally { Exit(); }
    }

    public void Close()
    {
        EnterOrThrow();
        try { CloseCore(); }
        finally { Exit(); }
    }

    private bool AreOffsetsValid(long head, long tail)
        => head >= 0 && tail >= 0 && head >= tail && head - tail <= Capacity;

    private bool IsReadyForRead()
    {
        if (Volatile.Read(ref _lifetimeState) < 0 || Volatile.Read(ref _header->IsClosed) != 0)
            return true;
        long head = Volatile.Read(ref _header->Head);
        long tail = Volatile.Read(ref _header->Tail);
        return !AreOffsetsValid(head, tail) || head > tail;
    }

    private void CloseCore()
    {
        if (Interlocked.Exchange(ref _header->IsClosed, 1) == 0)
        {
            _dataEvent.Set();
        }
    }

    private bool TryEnter()
    {
        int observed = Volatile.Read(ref _lifetimeState);
        while (observed >= 0)
        {
            int previous = Interlocked.CompareExchange(ref _lifetimeState, observed + 1, observed);
            if (previous == observed) return true;
            observed = previous;
        }
        return false;
    }

    private void EnterOrThrow()
    {
        if (!TryEnter()) throw new ObjectDisposedException(nameof(SharedMemoryWaveformBuffer));
    }

    private void Exit()
    {
        if (Interlocked.Decrement(ref _lifetimeState) != Disposing) return;
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();
        _dataEvent.Dispose();
    }

    public void Dispose()
    {
        if (!TryEnter()) return;
        try
        {
            int previous = Interlocked.Or(ref _lifetimeState, Disposing);
            if (previous >= 0) CloseCore();
        }
        finally { Exit(); }
    }
}
