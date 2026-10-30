using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Kable.Comparison.Scenarios;
namespace Kable.Comparison.Reporting;

public static class Results
{
    public static async Task WriteAsync(string output, List<Measurement> rows, int iterations, int warmup, int rounds)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(rows, options));
        var properties = typeof(Measurement).GetProperties();
        var csv = new StringBuilder(string.Join(",", properties.Select(p => p.Name)) + "\n");
        foreach (var row in rows)
            csv.AppendLine(string.Join(",", properties.Select(p => Convert.ToString(p.GetValue(row), CultureInfo.InvariantCulture))));
        await File.WriteAllTextAsync(Path.Combine(output, "results.csv"), csv.ToString());
        var versions = new Dictionary<string, string>();
        foreach (string assembly in new[] { "Kable", "TouchSocket", "SuperSocket.Client", "DotNetty.Transport" })
        {
            var loaded = Assembly.Load(assembly);
            versions[assembly] = loaded.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? loaded.GetName().Version!.ToString();
        }
        var environment = new { Utc = DateTimeOffset.UtcNow, OS = RuntimeInformation.OSDescription,
            Runtime = RuntimeInformation.FrameworkDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            CpuIdentifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
            LogicalProcessors = Environment.ProcessorCount, ServerGC = System.Runtime.GCSettings.IsServerGC,
            Iterations = iterations, WarmupPerConnection = warmup, Rounds = rounds, Versions = versions };
        await File.WriteAllTextAsync(Path.Combine(output, "environment.json"), JsonSerializer.Serialize(environment, options));
        var md = new StringBuilder("# TCP library comparison\n\n");
        md.AppendLine("Loopback ASCII+LF echo; one outstanding request per connection. Setup excluded. Each cell is the median of separate process rounds. Percentiles are per-request observations, not confidence intervals.");
        md.AppendLine("\nAllocation and CPU include client, in-process mock server and harness. No engine-only or zero-allocation claim. See environment.json and raw CSV for settings and individual rounds.\n");
        md.AppendLine("| Library | Bytes | Connections | p50 µs | p95 µs | p99 µs | requests/s | B/op | CPU ms |");
        md.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        double Median(IEnumerable<double> values) { var a = values.Order().ToArray(); return (a[(a.Length - 1) / 2] + a[a.Length / 2]) / 2; }
        foreach (var group in rows.GroupBy(r => (r.Library, r.Bytes, r.Connections)).OrderBy(g => g.Key.Bytes).ThenBy(g => g.Key.Connections).ThenBy(g => g.Key.Library))
            md.AppendLine(FormattableString.Invariant($"| {group.Key.Library} | {group.Key.Bytes} | {group.Key.Connections} | {Median(group.Select(r => r.P50Us)):F2} | {Median(group.Select(r => r.P95Us)):F2} | {Median(group.Select(r => r.P99Us)):F2} | {Median(group.Select(r => r.RequestsPerSecond)):F0} | {Median(group.Select(r => r.AllocatedBytesPerOperation)):F0} | {Median(group.Select(r => r.CpuMs)):F1} |"));
        await File.WriteAllTextAsync(Path.Combine(output, "results.md"), md.ToString());
    }
}
