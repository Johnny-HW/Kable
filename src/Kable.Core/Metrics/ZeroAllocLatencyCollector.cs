namespace Kable.Core.Metrics;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/// <summary>
/// A zero-allocation, pre-allocated circular buffer latency collector for ultra-low latency profiling.
/// Guarantees 0 B heap allocation during sample recording.
/// </summary>
public sealed class ZeroAllocLatencyCollector
{
    private readonly long[] _timestamps;
    private readonly int _capacity;
    private int _head;
    private int _count;

    public ZeroAllocLatencyCollector(int capacity = 131_072)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _capacity = capacity;
        _timestamps = new long[capacity];
    }

    public int Capacity => _capacity;
    public int Count => _count;

    /// <summary>
    /// Records an elapsed duration in stopwatch ticks without any heap allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordTicks(long elapsedTicks)
    {
        int index = _head;
        _timestamps[index] = elapsedTicks;
        _head = (index + 1) % _capacity;
        if (_count < _capacity)
        {
            _count++;
        }
    }

    /// <summary>
    /// Records elapsed duration from start timestamp to current timestamp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordDuration(long startTimestamp)
    {
        long elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        RecordTicks(elapsed);
    }

    /// <summary>
    /// Resets the collector state without releasing pre-allocated memory.
    /// </summary>
    public void Reset()
    {
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// Computes summary statistics from collected samples.
    /// Note: Analysis methods may allocate temporary arrays for sorting,
    /// so this should be called only after measurement completes.
    /// </summary>
    public LatencySummary ComputeSummary()
    {
        int count = _count;
        if (count == 0)
        {
            return new LatencySummary(0, 0, 0, 0, 0, 0);
        }

        long[] copy = new long[count];
        Array.Copy(_timestamps, copy, count);
        Array.Sort(copy);

        double tickFrequencyNs = (1_000_000_000.0 / Stopwatch.Frequency);

        int p50Index = (int)(count * 0.50);
        int p95Index = (int)(count * 0.95);
        int p99Index = (int)(count * 0.99);

        long p50Ticks = copy[Math.Min(p50Index, count - 1)];
        long p95Ticks = copy[Math.Min(p95Index, count - 1)];
        long p99Ticks = copy[Math.Min(p99Index, count - 1)];

        double sumTicks = 0;
        for (int i = 0; i < count; i++)
        {
            sumTicks += copy[i];
        }

        double meanTicks = sumTicks / count;

        double sumVariance = 0;
        for (int i = 0; i < count; i++)
        {
            double diff = copy[i] - meanTicks;
            sumVariance += diff * diff;
        }

        double stdDevTicks = Math.Sqrt(sumVariance / count);
        double cv = meanTicks > 0 ? (stdDevTicks / meanTicks) : 0.0;

        return new LatencySummary(
            Count: count,
            MeanNs: meanTicks * tickFrequencyNs,
            P50Ns: p50Ticks * tickFrequencyNs,
            P95Ns: p95Ticks * tickFrequencyNs,
            P99Ns: p99Ticks * tickFrequencyNs,
            CoefficientOfVariation: cv
        );
    }
}

public readonly record struct LatencySummary(
    int Count,
    double MeanNs,
    double P50Ns,
    double P95Ns,
    double P99Ns,
    double CoefficientOfVariation
);
