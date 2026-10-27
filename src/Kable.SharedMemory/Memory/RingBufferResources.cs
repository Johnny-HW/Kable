namespace Kable.SharedMemory.Memory;

using System.IO.MemoryMappedFiles;

internal unsafe sealed class RingBufferResources : IDisposable
{
    internal MemoryMappedFile? Mapping;
    internal MemoryMappedViewAccessor? View;
    internal EventWaitHandle? Data;
    internal EventWaitHandle? Space;
    internal byte* Pointer;
    private bool _acquired;
    private bool _transferred;

    internal void Map()
    {
        View = Mapping!.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        if (View.Capacity < 256) throw new InvalidDataException("The mapping is smaller than its header.");
        byte* pointer = null;
        View.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        _acquired = true;
        Pointer = pointer + View.PointerOffset;
    }

    internal static EventWaitHandle CreateEvent(string name)
    {
        var handle = new EventWaitHandle(false, EventResetMode.AutoReset, name, out bool created);
        if (created) return handle;
        handle.Dispose();
        throw new IOException("A shared memory event with this name already exists.");
    }

    internal void TransferOwnership() => _transferred = true;

    public void Dispose()
    {
        if (_transferred) return;
        Space?.Dispose();
        Data?.Dispose();
        if (_acquired) View!.SafeMemoryMappedViewHandle.ReleasePointer();
        View?.Dispose();
        Mapping?.Dispose();
    }
}
