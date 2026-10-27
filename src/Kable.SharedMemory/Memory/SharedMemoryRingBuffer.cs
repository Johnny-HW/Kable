namespace Kable.SharedMemory.Memory;

using System.IO.MemoryMappedFiles;
using System.Threading;

/// <summary>Single-producer/single-consumer byte ring in a named Windows memory mapping.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public unsafe sealed partial class SharedMemoryRingBuffer : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly MmfRingBufferHeader* _header;
    private readonly byte* _dataArea;
    private readonly EventWaitHandle _dataAvailableEvent;
    private readonly EventWaitHandle _spaceAvailableEvent;
    private int _lifetimeState;

    public int Capacity { get; }
    public int Mask { get; }

    private SharedMemoryRingBuffer(RingBufferResources resources, int? newCapacity)
    {
        _mmf = resources.Mapping!;
        _accessor = resources.View!;
        _header = (MmfRingBufferHeader*)resources.Pointer;
        _dataArea = resources.Pointer + 256;
        _dataAvailableEvent = resources.Data!;
        _spaceAvailableEvent = resources.Space!;
        if (newCapacity is int capacity)
        {
            Capacity = capacity;
            Mask = capacity - 1;
            _header->Version = MmfRingBufferHeader.CurrentVersion;
            _header->Capacity = Capacity;
            _header->Mask = Mask;
            Volatile.Write(ref _header->Head, 0);
            Volatile.Write(ref _header->Tail, 0);
            Volatile.Write(ref _header->IsClosed, 0);
            Volatile.Write(ref _header->Magic, MmfRingBufferHeader.ExpectedMagic);
        }
        else
        {
            if (Volatile.Read(ref _header->Magic) != MmfRingBufferHeader.ExpectedMagic ||
                _header->Version != MmfRingBufferHeader.CurrentVersion)
                throw new InvalidDataException("Unsupported or incomplete shared memory header.");
            Capacity = _header->Capacity;
            Mask = _header->Mask;
            if (Capacity <= 0 || (Capacity & (Capacity - 1)) != 0 || Mask != Capacity - 1 ||
                256L + Capacity > _accessor.Capacity)
                throw new InvalidDataException("Shared memory capacity or mask is outside the mapped view.");
            int closed = Volatile.Read(ref _header->IsClosed);
            if (closed is not (0 or 1)) throw new InvalidDataException("Invalid shared memory closure state.");
            if (closed == 1) throw new InvalidOperationException("The shared memory channel is closed.");
        }
        resources.TransferOwnership();
    }

    /// <summary>Creates a new physical ring. An existing mapping is never overwritten.</summary>
    public static SharedMemoryRingBuffer Create(string bufferName, int capacityPowerOfTwo = 64 * 1024)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bufferName);
        if (capacityPowerOfTwo <= 0 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
            throw new ArgumentException("Capacity must be a positive power of two.", nameof(capacityPowerOfTwo));
        using var resources = new RingBufferResources();
        resources.Mapping = MemoryMappedFile.CreateNew(bufferName + "_mmf", 256L + capacityPowerOfTwo,
            MemoryMappedFileAccess.ReadWrite);
        resources.Map();
        resources.Data = RingBufferResources.CreateEvent(bufferName + "_data_evt");
        resources.Space = RingBufferResources.CreateEvent(bufferName + "_space_evt");
        return new SharedMemoryRingBuffer(resources, capacityPowerOfTwo);
    }

    /// <summary>Opens and validates an existing physical ring without changing its shared state.</summary>
    public static SharedMemoryRingBuffer Open(string bufferName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bufferName);
        using var resources = new RingBufferResources();
        resources.Mapping = MemoryMappedFile.OpenExisting(bufferName + "_mmf", MemoryMappedFileRights.ReadWrite);
        resources.Map();
        resources.Data = EventWaitHandle.OpenExisting(bufferName + "_data_evt");
        resources.Space = EventWaitHandle.OpenExisting(bufferName + "_space_evt");
        return new SharedMemoryRingBuffer(resources, null);
    }
}
