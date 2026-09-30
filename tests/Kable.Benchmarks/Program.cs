namespace Kable.Benchmarks;

using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine(" Kable Industrial Engine - Baseline Performance Benchmarks       ");
        Console.WriteLine("=================================================================");

        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.ShortRun
                .WithWarmupCount(2)
                .WithIterationCount(4));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
