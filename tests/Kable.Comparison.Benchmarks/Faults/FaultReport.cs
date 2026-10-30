using System.Globalization;
using System.Text;
namespace Kable.Comparison.Faults;

public static class FaultReport
{
    public static async Task WriteAsync(string output, List<FaultRow> rows, List<ResourceRow> resources)
    {
        var md = new StringBuilder("# Fault comparison observations\n\n");
        md.AppendLine("raw = existing sample adapters, NOT exhaustive native-library testing. guarded = identical application policy for every library, including Kable. NotTested does not mean failure.\n");
        md.AppendLine("No winner score. SafeRejected preserves correctness by sacrificing reuse; CorrectReply preserves both. Caller cancellation deadline is not native timeout behavior. With 20 samples, p99 is the maximum. Latency excludes disposal, which is recorded separately.\n");
        md.AppendLine("| Mode | Library | Scenario | Pass/tested | Outcomes | p95 ms | p99 ms | max cleanup ms | max deadline overshoot ms |");
        md.AppendLine("|---|---|---|---:|---|---:|---:|---:|---:|");
        foreach (var group in rows.GroupBy(r => (r.Mode, r.Library, r.Scenario))
            .OrderBy(g => g.Key.Mode).ThenBy(g => g.Key.Library).ThenBy(g => g.Key.Scenario))
        {
            var tested = group.Where(r => r.Outcome != "NotTested").ToArray();
            string outcomes = string.Join("; ", group.GroupBy(r => r.Outcome).Select(g => $"{g.Key}={g.Count()}"));
            double Q(double p) { var a = tested.Select(r => r.LatencyMs).Order().ToArray(); return a.Length == 0 ? 0 : a[(int)Math.Ceiling(a.Length * p) - 1]; }
            md.AppendLine(FormattableString.Invariant($"| {group.Key.Mode} | {group.Key.Library} | {group.Key.Scenario} | {tested.Count(r => r.Passed)}/{tested.Length} | {outcomes} | {Q(.95):F3} | {Q(.99):F3} | {group.Max(r => r.CleanupMs):F3} | {group.Max(r => r.OvershootMs):F3} |"));
        }
        md.AppendLine("\n## Resources after repeated canceled connections\n");
        md.AppendLine("Full GC and 100ms settle between checkpoints. Process-local measurements include server/harness. Deltas are observations, not proof of leaks or leak freedom.\n");
        md.AppendLine("| Mode | Library | Cycles | managed delta B | private delta B | handle delta | thread delta |");
        md.AppendLine("|---|---|---:|---:|---:|---:|---:|");
        foreach (var group in resources.GroupBy(r => (r.Mode, r.Library)).OrderBy(g => g.Key.Mode).ThenBy(g => g.Key.Library))
        {
            var first = group.MinBy(r => r.Cycles)!;
            var last = group.MaxBy(r => r.Cycles)!;
            md.AppendLine($"| {group.Key.Mode} | {group.Key.Library} | {last.Cycles} | {last.ManagedBytes-first.ManagedBytes} | {last.PrivateBytes-first.PrivateBytes} | {last.Handles-first.Handles} | {last.Threads-first.Threads} |");
        }
        await File.WriteAllTextAsync(Path.Combine(output, "fault-results.md"), md.ToString());
        foreach (var group in rows.GroupBy(r => (r.Library, r.Mode)))
            await CsvAsync(Path.Combine(output, $"trials-{group.Key.Library}-{group.Key.Mode}.csv"), group);
        await CsvAsync(Path.Combine(output, "resources.csv"), resources);
    }
    private static async Task CsvAsync<T>(string path, IEnumerable<T> rows)
    {
        var properties = typeof(T).GetProperties();
        string Escape(object? value)
        {
            string s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        var text = new StringBuilder(string.Join(",", properties.Select(p => Escape(p.Name))) + "\n");
        foreach (var row in rows) text.AppendLine(string.Join(",", properties.Select(p => Escape(p.GetValue(row)))));
        await File.WriteAllTextAsync(path, text.ToString());
    }
}
