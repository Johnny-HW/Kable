namespace Kable.Engine.Disruptor.Tests;

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

        DisruptorAllocationAssert.AssertZeroAllocation(() =>
        {
            ring.TryEnqueue(item);
            ring.TryDequeue(out _);
        }, iterations: 100_000);
    }
}

