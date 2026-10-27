using BenchmarkDotNet.Running;
using Kable.SharedMemory.Benchmarks;

if (args.Length == 3 && args[0] == "--idle")
    await IdleWaitProfile.RunAsync(args[1], int.Parse(args[2]));
else if (args.Length == 2 && args[0] == "--wait-cost")
    await WaitAllocationProfile.RunAsync(int.Parse(args[1]));
else
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
