using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Kable.Comparison.Adapters;
using Kable.Comparison.Reporting;
namespace Kable.Comparison.Scenarios;

public static class ComparisonRunner
{
    public static async Task RunAsync(string[] args)
    {
        if (args.Contains("--help"))
        {
            Console.WriteLine("TCP comparison: --iterations 10000 --warmup 200 --rounds 3 --output <directory>");
            Console.WriteLine("Iterations are total per scenario, rounded up for equal work per connection.");
            return;
        }
        string Get(string key, string fallback)
        {
            int index = Array.IndexOf(args, key);
            return index < 0 ? fallback : index + 1 < args.Length ? args[index + 1]
                : throw new ArgumentException($"Missing value for {key}");
        }
        int iterations = int.Parse(Get("--iterations", "10000"));
        int warmup = int.Parse(Get("--warmup", "200"));
        int rounds = int.Parse(Get("--rounds", "3"));
        if (iterations < 1 || warmup < 0 || rounds < 1) throw new ArgumentOutOfRangeException(nameof(args));
        string output = Path.GetFullPath(Get("--output", "artifacts/library-comparison"));
        Directory.CreateDirectory(output);
        string worker = Get("--worker", "");
        if (worker.Length > 0)
        {
            var rows = new List<Measurement>();
            foreach (int bytes in new[] { 32, 256, 1024 })
            foreach (int count in new[] { 1, 16, 64 })
            {
                Console.WriteLine($"{worker}: {bytes} bytes / {count} connections");
                rows.Add(await Measure.RunAsync(worker, int.Parse(Get("--round", "1")), bytes,
                    count, iterations, warmup));
            }
            await File.WriteAllTextAsync(Path.Combine(output, Get("--file", "worker.json")),
                JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        var all = new List<Measurement>();
        var random = new Random(20261030);
        for (int round = 1; round <= rounds; round++)
        foreach (string name in Clients.Names.OrderBy(_ => random.Next()))
        {
            string file = $"{name}-{round}.json";
            var info = new ProcessStartInfo("dotnet") { UseShellExecute = false };
            foreach (string value in new[] { Assembly.GetExecutingAssembly().Location,
                "--worker", name, "--round", round.ToString(), "--iterations", iterations.ToString(),
                "--warmup", warmup.ToString(), "--output", output, "--file", file })
                info.ArgumentList.Add(value);
            using var process = Process.Start(info) ?? throw new IOException("Cannot start worker");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) throw new InvalidOperationException($"{name} failed: {process.ExitCode}");
            all.AddRange(JsonSerializer.Deserialize<List<Measurement>>(
                await File.ReadAllTextAsync(Path.Combine(output, file)))!);
        }
        await Results.WriteAsync(output, all, iterations, warmup, rounds);
        Console.WriteLine($"Results: {Path.Combine(output, "results.md")}");
    }
}
