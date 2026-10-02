namespace Kable.Benchmarks;

using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine(" Kable Industrial Engine - Baseline Performance Benchmarks       ");
        Console.WriteLine("=================================================================");

        if (args.Length > 0 && args[0].Equals("--latency-profile", StringComparison.OrdinalIgnoreCase))
        {
            int iterations = 10_000;
            if (args.Length > 1 && int.TryParse(args[1], out int customIter))
            {
                iterations = customIter;
            }

            Console.WriteLine($"Running Zero-Alloc Latency Profile ({iterations:N0} iterations)...");
            var summary = await LatencyProfileRunner.RunSessionLatencyProfileAsync(iterations);

            Console.WriteLine("-----------------------------------------------------------------");
            Console.WriteLine($" Sample Count : {summary.Count:N0}");
            Console.WriteLine($" Mean Latency : {summary.MeanNs / 1000.0:F2} μs ({summary.MeanNs:F0} ns)");
            Console.WriteLine($" P50 Latency  : {summary.P50Ns / 1000.0:F2} μs ({summary.P50Ns:F0} ns)");
            Console.WriteLine($" P95 Latency  : {summary.P95Ns / 1000.0:F2} μs ({summary.P95Ns:F0} ns)");
            Console.WriteLine($" P99 Latency  : {summary.P99Ns / 1000.0:F2} μs ({summary.P99Ns:F0} ns)");
            Console.WriteLine($" Coeff. of Var: {summary.CoefficientOfVariation:F4} (CV = StdDev / Mean)");
            Console.WriteLine("-----------------------------------------------------------------");
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

