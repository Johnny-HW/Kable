namespace Kable.SharedMemory.Waveform;

using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// 고주파 아날로그 파형 및 계측 텔레메트리를 위한 슬롯 구조체 (샘플 타임스탬프, 채널 ID, 값)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct WaveformSample
{
    public long TimestampTicks;
    public int ChannelId;
    public double Value;

    public WaveformSample(int channelId, double value, long timestampTicks = 0)
    {
        ChannelId = channelId;
        Value = value;
        TimestampTicks = timestampTicks == 0 ? DateTime.UtcNow.Ticks : timestampTicks;
    }
}

/// <summary>
/// 파형 버퍼 MMF 헤더
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 256)]
public struct WaveformBufferHeader
{
    public const uint MagicVal = 0x57415645; // 'WAVE'

    [FieldOffset(0)]
    public uint Magic;

    [FieldOffset(4)]
    public int Capacity;

    [FieldOffset(8)]
    public int Mask;

    [FieldOffset(64)]
    public long Head;

    [FieldOffset(128)]
    public long Tail;

    [FieldOffset(192)]
    public int IsClosed;
}

/// <summary>
/// 초당 수천~수만 회의 고주파 파형 샘플을 Zero-Copy로 교환하는 SPSC MMF 파형 버퍼.
/// </summary>
#if NET5_0_OR_GREATER
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
public unsafe sealed partial class SharedMemoryWaveformBuffer : IDisposable
{
    private const int Disposing = int.MinValue;
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly byte* _pointer;
    private readonly WaveformBufferHeader* _header;
    private readonly WaveformSample* _samples;
    private readonly EventWaitHandle _dataEvent;
    private int _lifetimeState;

    public int Capacity { get; }
    public int Mask { get; }
    public bool IsClosed
    {
        get
        {
            EnterOrThrow();
            try { return Volatile.Read(ref _header->IsClosed) != 0; }
            finally { Exit(); }
        }
    }

    private SharedMemoryWaveformBuffer(
        MemoryMappedFile mmf,
        MemoryMappedViewAccessor accessor,
        byte* pointer,
        EventWaitHandle dataEvent,
        bool isCreator,
        int requestedCapacity)
    {
        _mmf = mmf;
        _accessor = accessor;
        _pointer = pointer;
        _header = (WaveformBufferHeader*)pointer;
        _samples = (WaveformSample*)(pointer + 256);
        _dataEvent = dataEvent;

        if (isCreator)
        {
            Capacity = requestedCapacity;
            Mask = requestedCapacity - 1;

            _header->Capacity = Capacity;
            _header->Mask = Mask;
            Volatile.Write(ref _header->Head, 0);
            Volatile.Write(ref _header->Tail, 0);
            Volatile.Write(ref _header->IsClosed, 0);
            Volatile.Write(ref _header->Magic, WaveformBufferHeader.MagicVal);
        }
        else
        {
            if (Volatile.Read(ref _header->Magic) != WaveformBufferHeader.MagicVal)
            {
                throw new InvalidOperationException("Invalid waveform buffer magic.");
            }
            Capacity = _header->Capacity;
            Mask = _header->Mask;
            if (Capacity <= 0 || (Capacity & (Capacity - 1)) != 0 || Mask != Capacity - 1 ||
                accessor.Capacity < 256L + (long)Capacity * sizeof(WaveformSample))
                throw new InvalidOperationException("Invalid waveform buffer capacity or mapping size.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWrite(in WaveformSample sample)
    {
        EnterOrThrow();
        try
        {
            if (Volatile.Read(ref _header->IsClosed) != 0) return false;

            long head = Volatile.Read(ref _header->Head);
            long tail = Volatile.Read(ref _header->Tail);
            if (!AreOffsetsValid(head, tail))
            {
                CloseCore();
                return false;
            }

            if (head - tail >= Capacity) return false; // Buffer full

            _samples[head & Mask] = sample;
            Volatile.Write(ref _header->Head, head + 1);
            _dataEvent.Set();
            return true;
        }
        finally { Exit(); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteBatch(ReadOnlySpan<WaveformSample> samples)
    {
        EnterOrThrow();
        try
        {
            if (samples.IsEmpty || Volatile.Read(ref _header->IsClosed) != 0) return 0;

            long head = Volatile.Read(ref _header->Head);
            long tail = Volatile.Read(ref _header->Tail);
            if (!AreOffsetsValid(head, tail))
            {
                CloseCore();
                return 0;
            }

            long available = Capacity - (head - tail);
            if (available <= 0) return 0;

            int toWrite = (int)Math.Min(samples.Length, available);
            for (int i = 0; i < toWrite; i++)
            {
                _samples[(head + i) & Mask] = samples[i];
            }

            Volatile.Write(ref _header->Head, head + toWrite);
            _dataEvent.Set();
            return toWrite;
        }
        finally { Exit(); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(out WaveformSample sample)
    {
        EnterOrThrow();
        try
        {
            long tail = Volatile.Read(ref _header->Tail);
            long head = Volatile.Read(ref _header->Head);
            if (!AreOffsetsValid(head, tail))
            {
                CloseCore();
                sample = default;
                return false;
            }

            if (tail >= head)
            {
                sample = default;
                return false;
            }

            sample = _samples[tail & Mask];
            Volatile.Write(ref _header->Tail, tail + 1);
            return true;
        }
        finally { Exit(); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadBatch(Span<WaveformSample> destination)
    {
        EnterOrThrow();
        try
        {
            if (destination.IsEmpty) return 0;

            long tail = Volatile.Read(ref _header->Tail);
            long head = Volatile.Read(ref _header->Head);
            if (!AreOffsetsValid(head, tail))
            {
                CloseCore();
                return 0;
            }

            long available = head - tail;
            if (available <= 0) return 0;

            int toRead = (int)Math.Min(destination.Length, available);
            for (int i = 0; i < toRead; i++)
            {
                destination[i] = _samples[(tail + i) & Mask];
            }

            Volatile.Write(ref _header->Tail, tail + toRead);
            return toRead;
        }
        finally { Exit(); }
    }
}
