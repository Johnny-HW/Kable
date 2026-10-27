namespace Kable.SharedMemory.Benchmarks;

using System.Diagnostics;

internal static partial class TransportProfile
{
    private static async Task RunRequestsAsync(ProfileLink link, Histogram histogram, int operations, long deadline, bool burst)
    {
        for (int i = 0; (operations == 0 || i < operations) && Stopwatch.GetTimestamp() < deadline; i++)
        {
            long before = Stopwatch.GetTimestamp();
            await link.QueryAsync();
            histogram.Record(Stopwatch.GetElapsedTime(before).TotalMicroseconds);
            if (burst && i % 32 == 31) await Task.Delay(1);
        }
    }

    private static async Task RunStreamAsync(ProfileLink link, int operations, Histogram histogram, CancellationToken ct)
    {
        Task receive = ConsumeAsync();
        for (int i = 0; i < operations; i++) await link.Server.Output.WriteAsync(link.Telemetry, ct);
        await receive;

        async Task ConsumeAsync()
        {
            int received = 0;
            await foreach (string message in link.Session.GetStreamAsync(ct))
            {
                if (message != link.TelemetryText) throw new InvalidDataException("Stream payload mismatch.");
                histogram.Record(0);
                if (++received == operations) break;
            }
        }
    }

    private sealed class Histogram
    {
        private readonly long[] _counts = new long[100001];
        internal long Count;
        internal long Above100Ms;
        internal double TotalMicroseconds;
        internal double MaxMicroseconds;

        internal void Record(double microseconds)
        {
            Count++;
            TotalMicroseconds += microseconds;
            MaxMicroseconds = Math.Max(MaxMicroseconds, microseconds);
            if (microseconds > 100000) Above100Ms++;
            _counts[(int)Math.Min(100000, microseconds)]++;
        }

        internal double? Percentile(double percentile)
        {
            long target = (long)Math.Ceiling(Count * percentile);
            long seen = 0;
            for (int i = 0; i < _counts.Length; i++)
            {
                seen += _counts[i];
                if (seen >= target) return i;
            }
            return null;
        }

        internal static Histogram Combine(IEnumerable<Histogram> histograms)
        {
            var combined = new Histogram();
            foreach (var histogram in histograms)
            {
                combined.Count += histogram.Count;
                combined.TotalMicroseconds += histogram.TotalMicroseconds;
                combined.MaxMicroseconds = Math.Max(combined.MaxMicroseconds, histogram.MaxMicroseconds);
                combined.Above100Ms += histogram.Above100Ms;
                for (int i = 0; i < combined._counts.Length; i++) combined._counts[i] += histogram._counts[i];
            }
            return combined;
        }
    }
}
