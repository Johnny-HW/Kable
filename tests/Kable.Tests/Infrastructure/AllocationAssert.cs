namespace Kable.Tests.Infrastructure;

using System;
using System.Diagnostics;
using FluentAssertions;

public static class AllocationAssert
{
    /// <summary>
    /// Executes an action after JIT warmup and asserts that zero bytes were allocated on the managed heap.
    /// </summary>
    public static void AssertZeroAllocation(Action action, int iterations = 1000)
    {
        // 1. Warm up JIT, cache, and static constructors
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
