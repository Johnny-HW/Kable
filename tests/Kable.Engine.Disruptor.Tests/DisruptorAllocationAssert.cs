namespace Kable.Engine.Disruptor.Tests;

using System;
using FluentAssertions;

public static class DisruptorAllocationAssert
{
    public static void AssertZeroAllocation(Action action, int iterations = 10_000)
    {
        action();
        action();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startBytes = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < iterations; i++)
        {
            action();
        }

        long endBytes = GC.GetAllocatedBytesForCurrentThread();
        long allocated = endBytes - startBytes;

        allocated.Should().Be(0, $"Expected 0 bytes allocated across {iterations} iterations, but found {allocated} bytes.");
    }
}
