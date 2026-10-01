namespace Kable.Benchmarks;

using System;
using BenchmarkDotNet.Running;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine(" Kable Industrial Engine - Baseline Performance Benchmarks       ");
        Console.WriteLine("=================================================================");

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
