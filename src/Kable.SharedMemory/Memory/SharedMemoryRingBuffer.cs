namespace Kable.SharedMemory.Memory;

using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// MMF 내부에 직접 배치되는 헤더 구조체 (64바이트 캐시라인 정렬).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 256)]
public struct MmfRingBufferHeader
{
    public const uint ExpectedMagic = 0x4B41424C; // 'KABL'
    public const ushort CurrentVersion = 1;

    [FieldOffset(0)]
    public uint Magic;

    [FieldOffset(4)]
    public ushort Version;

    [FieldOffset(8)]
    public int Capacity;

    [FieldOffset(12)]
    public int Mask;

    // Cache line 1: Producer Write Offset
    [FieldOffset(64)]
    public long Head;

    // Cache line 2: Consumer Read Offset
    [FieldOffset(128)]
    public long Tail;

    // Cache line 3: State flags (0: Active, 1: Closed/Aborted)
    [FieldOffset(192)]
    public int IsClosed;
}

/// <summary>
/// 프로세스 간 공유 메모리(MMF) 기반의 Lock-Free SPSC 원형 바이트 버퍼.
/// </summary>
public unsafe sealed class SharedMemoryRingBuffer : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly byte* _pointer;
    private readonly MmfRingBufferHeader* _header;
    private readonly byte* _dataArea;
    private readonly EventWaitHandle _dataAvailableEvent;
    private readonly EventWaitHandle _spaceAvailableEvent;
    private readonly bool _ownsMmf;
    private int _isDisposed;

    public int Capacity { get; }
    public int Mask { get; }
    public bool IsClosed => Volatile.Read(ref _header->IsClosed) != 0;

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
        _pointer = pointer;
        _header = (MmfRingBufferHeader*)pointer;
        _dataArea = pointer + 256; // 256 바이트 헤더 이후 데이터 영역
        _dataAvailableEvent = dataAvailableEvent;
        _spaceAvailableEvent = spaceAvailableEvent;
        _ownsMmf = isCreator;

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
        if ((capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
        {
            throw new ArgumentException("Capacity must be a power of two.", nameof(capacityPowerOfTwo));
        }

        long totalSize = 256 + capacityPowerOfTwo;
        string mmfName = $"{bufferName}_mmf";
        string dataEvtName = $"{bufferName}_data_evt";
        string spaceEvtName = $"{bufferName}_space_evt";

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
        string mmfName = $"{bufferName}_mmf";
        string dataEvtName = $"{bufferName}_data_evt";
        string spaceEvtName = $"{bufferName}_space_evt";

        var mmf = MemoryMappedFile.OpenExisting(mmfName, MemoryMappedFileRights.ReadWrite);
        var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

        byte* ptr = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

        var dataEvt = EventWaitHandle.OpenExisting(dataEvtName);
        var spaceEvt = EventWaitHandle.OpenExisting(spaceEvtName);

        return new SharedMemoryRingBuffer(mmf, accessor, ptr, dataEvt, spaceEvt, isCreator: false, 0);
    }

    /// <summary>
    /// 버퍼에 바이트 데이터를 기록(Produce)합니다.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Write(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty || IsClosed) return 0;

        long head = Volatile.Read(ref _header->Head);
        long tail = Volatile.Read(ref _header->Tail);

        long availableSpace = Capacity - (head - tail);
        if (availableSpace <= 0) return 0;

        int bytesToWrite = (int)Math.Min(source.Length, availableSpace);
        int headIndex = (int)(head & Mask);

        // 원형 버퍼 랩어라운드(Wrap-around) 복사 처리
        int firstChunk = Math.Min(bytesToWrite, Capacity - headIndex);
        source.Slice(0, firstChunk).CopyTo(new Span<byte>(_dataArea + headIndex, firstChunk));

        int secondChunk = bytesToWrite - firstChunk;
        if (secondChunk > 0)
        {
            source.Slice(firstChunk, secondChunk).CopyTo(new Span<byte>(_dataArea, secondChunk));
        }

        Volatile.Write(ref _header->Head, head + bytesToWrite);
        _dataAvailableEvent.Set();

        return bytesToWrite;
    }

    /// <summary>
    /// 버퍼에서 바이트 데이터를 읽어(Consume) 대상 span에 복사합니다.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Read(Span<byte> destination)
    {
        if (destination.IsEmpty) return 0;

        long tail = Volatile.Read(ref _header->Tail);
        long head = Volatile.Read(ref _header->Head);

        long availableData = head - tail;
        if (availableData <= 0) return 0;

        int bytesToRead = (int)Math.Min(destination.Length, availableData);
        int tailIndex = (int)(tail & Mask);

        int firstChunk = Math.Min(bytesToRead, Capacity - tailIndex);
        new ReadOnlySpan<byte>(_dataArea + tailIndex, firstChunk).CopyTo(destination.Slice(0, firstChunk));

        int secondChunk = bytesToRead - firstChunk;
        if (secondChunk > 0)
        {
            new ReadOnlySpan<byte>(_dataArea, secondChunk).CopyTo(destination.Slice(firstChunk, secondChunk));
        }

        Volatile.Write(ref _header->Tail, tail + bytesToRead);
        _spaceAvailableEvent.Set();

        return bytesToRead;
    }

    /// <summary>
    /// 데이터가 유입될 때까지 대기합니다. (비동기 친화적 하이브리드 대기)
    /// </summary>
    public bool WaitForData(int timeoutMs, CancellationToken ct = default)
    {
        long tail = Volatile.Read(ref _header->Tail);
        long head = Volatile.Read(ref _header->Head);
        if (head > tail || IsClosed) return true;

        using var waitHandleRegistration = ct.CanBeCanceled 
            ? ct.Register(() => _dataAvailableEvent.Set()) 
            : default;

        int result = WaitHandle.WaitAny(new WaitHandle[] { _dataAvailableEvent }, timeoutMs);
        ct.ThrowIfCancellationRequested();

        return result == 0;
    }

    /// <summary>
    /// 버퍼 공간이 생길 때까지 대기합니다.
    /// </summary>
    public bool WaitForSpace(int timeoutMs, CancellationToken ct = default)
    {
        long head = Volatile.Read(ref _header->Head);
        long tail = Volatile.Read(ref _header->Tail);
        if (Capacity - (head - tail) > 0 || IsClosed) return true;

        using var waitHandleRegistration = ct.CanBeCanceled 
            ? ct.Register(() => _spaceAvailableEvent.Set()) 
            : default;

        int result = WaitHandle.WaitAny(new WaitHandle[] { _spaceAvailableEvent }, timeoutMs);
        ct.ThrowIfCancellationRequested();

        return result == 0;
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref _header->IsClosed, 1) == 0)
        {
            _dataAvailableEvent.Set();
            _spaceAvailableEvent.Set();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

        Close();

        if (_pointer != null)
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }

        _accessor.Dispose();
        _mmf.Dispose();
        _dataAvailableEvent.Dispose();
        _spaceAvailableEvent.Dispose();
    }
}
