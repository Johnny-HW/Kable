namespace Kable.SharedMemory.WaitDiagnostics;

using System.Diagnostics;

internal sealed class Distribution
{
    private readonly long[] _counts = new long[10001];
    private long _totalTicks;
    private int _count;
    private double _max;
    private int _overflow;

    internal void Add(long ticks)
    {
        if (ticks < 0) throw new InvalidOperationException("Diagnostic timestamps are out of order.");
        double us = (double)ticks * 1000000 / Stopwatch.Frequency;
        _counts[(int)Math.Min(10000, us * 10)]++;
        _totalTicks += ticks;
        _max = Math.Max(_max, us);
        if (us >= 1000) _overflow++;
        _count++;
    }

    internal object Summary() => new
    {
        Count = _count, MeanUs = (double)_totalTicks * 1000000 / Stopwatch.Frequency / _count,
        P50Us = Percentile(0.5), P95Us = Percentile(0.95), P99Us = Percentile(0.99), MaxUs = _max,
        AtLeast1Ms = _overflow
    };

    private double Percentile(double quantile)
    {
        long seen = 0;
        long target = (long)Math.Ceiling(_count * quantile);
        for (int i = 0; i < _counts.Length; i++)
        {
            seen += _counts[i];
            if (seen >= target) return i / 10.0;
        }
        return 0;
    }
}
