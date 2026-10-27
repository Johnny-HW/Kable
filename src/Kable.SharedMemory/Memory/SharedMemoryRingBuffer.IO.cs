namespace Kable.SharedMemory.Memory;

using System;
using System.Runtime.CompilerServices;
using System.Threading;

public unsafe sealed partial class SharedMemoryRingBuffer
{
    /// <summary>
    /// 버퍼에 바이트 데이터를 기록(Produce)합니다.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Write(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty || !TryEnter()) return 0;

        try
        {
            if (Volatile.Read(ref _header->IsClosed) != 0)
            {
                return 0;
            }

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
        finally
        {
            Exit();
        }
    }

    /// <summary>
    /// 버퍼에서 바이트 데이터를 읽어(Consume) 대상 span에 복사합니다.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Read(Span<byte> destination)
    {
        if (destination.IsEmpty || !TryEnter()) return 0;

        try
        {
            if (Volatile.Read(ref _header->IsClosed) != 0)
            {
                return 0;
            }

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
        finally
        {
            Exit();
        }
    }

}
