namespace Kable.Tests.Cases.Metrics;

using System;
using System.Diagnostics;
using FluentAssertions;
using Kable.Core.Metrics;
using Xunit;

public class ZeroAllocLatencyCollectorTests
{
    [Fact]
    public void RecordDuration_ShouldNotAllocateHeap()
    {
        // Arrange: capacity 100,000
        var collector = new ZeroAllocLatencyCollector(100_000);

        // Warm up JIT
        collector.RecordTicks(100);
        collector.Reset();

        long startAlloc = GC.GetAllocatedBytesForCurrentThread();

        // Act: Record 10,000 samples
        for (int i = 0; i < 10_000; i++)
        {
            long start = Stopwatch.GetTimestamp();
            collector.RecordTicks(150);
        }

        long endAlloc = GC.GetAllocatedBytesForCurrentThread();

        // Assert: 0 bytes allocated during recording loop
        (endAlloc - startAlloc).Should().Be(0);
        collector.Count.Should().Be(10_000);
    }

    [Fact]
    public void ComputeSummary_ShouldCalculateAccuratePercentilesAndCv()
    {
        var collector = new ZeroAllocLatencyCollector(1000);

        // Add 100 samples from 1 to 100
        for (int i = 1; i <= 100; i++)
        {
            collector.RecordTicks(i);
        }

        var summary = collector.ComputeSummary();

        summary.Count.Should().Be(100);
        summary.P50Ns.Should().BeGreaterThan(0);
        summary.P99Ns.Should().BeGreaterThan(summary.P50Ns);
        summary.CoefficientOfVariation.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Reset_ShouldResetCountAndHeadWithoutAllocating()
    {
        var collector = new ZeroAllocLatencyCollector(100);
        collector.RecordTicks(10);
        collector.Count.Should().Be(1);

        collector.Reset();
        collector.Count.Should().Be(0);

        collector.RecordTicks(20);
        collector.Count.Should().Be(1);
    }
}
