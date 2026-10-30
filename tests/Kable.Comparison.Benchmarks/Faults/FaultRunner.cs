using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Kable.Comparison.Adapters;
namespace Kable.Comparison.Faults;

public sealed record FaultRun(List<FaultRow> Trials, List<ResourceRow> Resources);

public static class FaultRunner
{
    public static async Task RunAsync(string[] args)
    {
        string Get(string key, string fallback)
        {
            int index = Array.IndexOf(args, key);
            return index < 0 ? fallback : index + 1 < args.Length ? args[index + 1]
                : throw new ArgumentException($"Missing value for {key}");
        }
        string output = Path.GetFullPath(Get("--output", "artifacts/fault-comparison"));
        int trials = int.Parse(Get("--trials", "20")), cycles = int.Parse(Get("--cycles", "100"));
        if (trials < 1 || cycles < 4) throw new ArgumentOutOfRangeException(nameof(args));
        Directory.CreateDirectory(output);
        string worker = Get("--fault-worker", "");
        var options = new JsonSerializerOptions { WriteIndented = true };
        if (worker.Length > 0)
        {
            string mode = Get("--mode", "raw");
            // Keep runtime infrastructure alive across reconnects; this holds NO TCP connection.
            await using var runtimeLease = worker == "DotNetty" ? new DotNettyClient() : null;
            var rows = new List<FaultRow>();
            foreach (string scenario in FaultProbes.Scenarios)
            {
                Console.WriteLine($"{worker}/{mode}: {scenario}");
                for (int i = 1; i <= trials; i++)
                    rows.Add(scenario == "queued-disconnect" && mode == "raw"
                        ? new(worker, mode, scenario, i, "NotTested", "Raw adapter has no common concurrency contract", 0, 0, 0, "")
                        : await FaultProbes.RunAsync(worker, mode, scenario, i));
            }
            Console.WriteLine($"{worker}/{mode}: resource cycles {cycles}");
            var resources = await ResourceProbes.RunAsync(worker, mode, cycles);
            await File.WriteAllTextAsync(Path.Combine(output, $"{worker}-{mode}.json"),
                JsonSerializer.Serialize(new FaultRun(rows, resources), options));
            return;
        }
        var all = new List<FaultRow>();
        var resourceRows = new List<ResourceRow>();
        var random = new Random(20261030);
        var jobs = Clients.Names.SelectMany(name => new[] { "raw", "guarded" }.Select(mode => (name, mode)))
            .OrderBy(_ => random.Next()).ToArray();
        foreach (var (name, mode) in jobs)
        {
            var info = new ProcessStartInfo("dotnet") { UseShellExecute = false };
            foreach (string value in new[] { Assembly.GetExecutingAssembly().Location, "--faults",
                "--fault-worker", name, "--mode", mode, "--trials", trials.ToString(),
                "--cycles", cycles.ToString(), "--output", output }) info.ArgumentList.Add(value);
            using var process = Process.Start(info) ?? throw new IOException("Cannot start worker");
            using var watchdog = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            try { await process.WaitForExitAsync(watchdog.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
            if (process.ExitCode != 0) throw new InvalidOperationException($"Worker failed: {name}/{mode} {process.ExitCode}");
            var run = JsonSerializer.Deserialize<FaultRun>(await File.ReadAllTextAsync(
                Path.Combine(output, $"{name}-{mode}.json")))!;
            all.AddRange(run.Trials);
            resourceRows.AddRange(run.Resources);
        }
        await FaultReport.WriteAsync(output, all, resourceRows);
        await File.WriteAllTextAsync(Path.Combine(output, "environment.json"), JsonSerializer.Serialize(new
        {
            Utc = DateTimeOffset.UtcNow, OS = RuntimeInformation.OSDescription,
            Runtime = RuntimeInformation.FrameworkDescription,
            LogicalProcessors = Environment.ProcessorCount, TrialsPerScenario = trials,
            ResourceCyclesPerWorker = cycles, ServerGC = System.Runtime.GCSettings.IsServerGC,
            Versions = new Dictionary<string, string?>
            {
                ["Kable"] = Assembly.Load("Kable").GetName().Version?.ToString(),
                ["TouchSocket"] = Assembly.Load("TouchSocket").GetName().Version?.ToString(),
                ["SuperSocket.Client"] = Assembly.Load("SuperSocket.Client").GetName().Version?.ToString(),
                ["DotNetty.Transport"] = Assembly.Load("DotNetty.Transport").GetName().Version?.ToString()
            }
        }, options));
        Console.WriteLine($"Fault report: {Path.Combine(output, "fault-results.md")}");
    }
}
