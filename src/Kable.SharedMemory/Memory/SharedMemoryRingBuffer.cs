namespace Kable.SharedMemory.Memory;

using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
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

    private SharedMemoryRingBuffer(
        MemoryMappedFile mmf,
        MemoryMappedViewAccessor accessor,
        byte* pointer,
        EventWaitHandle dataAvailableEvent,
        EventWaitHandle spaceAvailableEvent,
        bool isCreator,
        int requestedCapacityPowerOfTwo)
    {
        _mmf = mmf;
        _accessor = accessor;
        _header = (MmfRingBufferHeader*)pointer;
        _dataArea = pointer + 256; // 256 바이트 헤더 이후 데이터 영역
        _dataAvailableEvent = dataAvailableEvent;
        _spaceAvailableEvent = spaceAvailableEvent;

        if (isCreator)
        {
            Capacity = requestedCapacityPowerOfTwo;
            Mask = requestedCapacityPowerOfTwo - 1;

            _header->Magic = MmfRingBufferHeader.ExpectedMagic;
            _header->Version = MmfRingBufferHeader.CurrentVersion;
            _header->Capacity = Capacity;
            _header->Mask = Mask;
            Volatile.Write(ref _header->Head, 0);
            Volatile.Write(ref _header->Tail, 0);
            Volatile.Write(ref _header->IsClosed, 0);
        }
        else
        {
            if (_header->Magic != MmfRingBufferHeader.ExpectedMagic)
            {
                throw new InvalidOperationException("Invalid shared memory segment magic number.");
            }
            Capacity = _header->Capacity;
            Mask = _header->Mask;
        }
    }

    /// <summary>
    /// 지정된 이름의 공유 메모리 링 버퍼를 생성(서버/생성자)합니다.
    /// </summary>
    public static SharedMemoryRingBuffer Create(string bufferName, int capacityPowerOfTwo = 64 * 1024)
    {
        if (capacityPowerOfTwo <= 0 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
        {
            throw new ArgumentException("Capacity must be a power of two.", nameof(capacityPowerOfTwo));
        }

        long totalSize = 256 + capacityPowerOfTwo;
        var (mmfName, dataEvtName, spaceEvtName) = GetKernelObjectNames(bufferName);

        var mmf = MemoryMappedFile.CreateOrOpen(mmfName, totalSize, MemoryMappedFileAccess.ReadWrite);
        var accessor = mmf.CreateViewAccessor(0, totalSize, MemoryMappedFileAccess.ReadWrite);

        byte* ptr = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

        var dataEvt = new EventWaitHandle(false, EventResetMode.AutoReset, dataEvtName);
        var spaceEvt = new EventWaitHandle(false, EventResetMode.AutoReset, spaceEvtName);

        return new SharedMemoryRingBuffer(mmf, accessor, ptr, dataEvt, spaceEvt, isCreator: true, capacityPowerOfTwo);
    }

    /// <summary>
    /// 기존에 생성된 공유 메모리 링 버퍼에 연결(클라이언트/소비자)합니다.
    /// </summary>
    public static SharedMemoryRingBuffer Open(string bufferName)
    {
        var (mmfName, dataEvtName, spaceEvtName) = GetKernelObjectNames(bufferName);

        var mmf = MemoryMappedFile.OpenExisting(mmfName, MemoryMappedFileRights.ReadWrite);
        var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

        byte* ptr = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

        var dataEvt = EventWaitHandle.OpenExisting(dataEvtName);
        var spaceEvt = EventWaitHandle.OpenExisting(spaceEvtName);

        return new SharedMemoryRingBuffer(mmf, accessor, ptr, dataEvt, spaceEvt, isCreator: false, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (string MmfName, string DataEvtName, string SpaceEvtName) GetKernelObjectNames(string bufferName)
        => ($"{bufferName}_mmf", $"{bufferName}_data_evt", $"{bufferName}_space_evt");

}
