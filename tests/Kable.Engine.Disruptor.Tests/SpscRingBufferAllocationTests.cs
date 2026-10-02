namespace Kable.Engine.Disruptor.Tests;

using System;
using FluentAssertions;
using Kable.Engine.Disruptor;
using Xunit;

public sealed class SpscRingBufferAllocationTests
{
    private sealed class PooledPacket
    {
        public int Id { get; set; }
    }

    [Fact]
    public void SpscRingBuffer_EnqueueDequeueLoop_ShouldAllocateZeroBytes()
    {
        var ring = new SpscRingBuffer<PooledPacket>(1024);
        var item = new PooledPacket { Id = 42 };

        // Warm up JIT and cache
        ring.TryEnqueue(item);
        ring.TryDequeue(out _);

        long startAlloc = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 100_000; i++)
        {
            ring.TryEnqueue(item);
            ring.TryDequeue(out _);
        }

        long endAlloc = GC.GetAllocatedBytesForCurrentThread();

        // Must be exactly 0 bytes allocated during 100,000 operations
        (endAlloc - startAlloc).Should().Be(0);
    }
}
