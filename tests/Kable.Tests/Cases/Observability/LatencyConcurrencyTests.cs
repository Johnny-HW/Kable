namespace Kable.Tests.Cases.Observability;

using Kable.Core.Metrics;
using Xunit;

public sealed class LatencyConcurrencyTests
{
    [Fact]
    public void ConcurrentWritersKeepEverySampleBeforeCapacity()
    {
        const int count = 50000;
        var collector = new ZeroAllocLatencyCollector(count);
        Parallel.For(0, count, _ => collector.RecordTicks(100));
        Assert.Equal(count, collector.Count);
        var summary = collector.ComputeSummary();
        Assert.Equal(count, summary.Count);
        Assert.Equal(0, summary.CoefficientOfVariation);
    }

    [Fact]
    public void SnapshotAndResetAreSafeDuringRecording()
    {
        var collector = new ZeroAllocLatencyCollector(8);
        Parallel.Invoke(
            () => { for (int i = 0; i < 10000; i++) collector.RecordTicks(100); },
            () => { for (int i = 0; i < 1000; i++) Assert.InRange(collector.ComputeSummary().Count, 0, 8); },
            () => { for (int i = 0; i < 1000; i++) collector.Reset(); });
    }
}
