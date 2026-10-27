namespace Kable.SharedMemory.Memory;

using System.Threading;

public unsafe sealed partial class SharedMemoryRingBuffer
{
    private const int Disposing = int.MinValue;

    public bool IsClosed
    {
        get
        {
            if (!TryEnter()) return true;
            try { return Volatile.Read(ref _header->IsClosed) != 0; }
            finally { Exit(); }
        }
    }

    // One atomic word makes entering an operation and publishing disposal mutually exclusive.
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

    private void Exit()
    {
        if (Interlocked.Decrement(ref _lifetimeState) != Disposing) return;
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();
        _dataAvailableEvent.Dispose();
        _spaceAvailableEvent.Dispose();
    }

    // Caller holds an operation lease throughout the pointer and event accesses.
    private bool IsReady(bool space)
    {
        if (Volatile.Read(ref _lifetimeState) < 0 || Volatile.Read(ref _header->IsClosed) != 0)
            return true;
        long head = Volatile.Read(ref _header->Head);
        long tail = Volatile.Read(ref _header->Tail);
        return space ? head - tail < Capacity : head > tail;
    }

    public void Close()
    {
        if (!TryEnter()) return;
        try { CloseCore(); }
        finally { Exit(); }
    }

    private void CloseCore()
    {
        if (Interlocked.Exchange(ref _header->IsClosed, 1) != 0) return;
        _dataAvailableEvent.Set();
        _spaceAvailableEvent.Set();
    }

    /// <summary>Closes immediately; the last in-flight operation releases the mapping and handles.</summary>
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
