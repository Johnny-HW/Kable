using BenchmarkDotNet.Running;
using Kable.SharedMemory.Benchmarks;

if (args.Length == 3 && args[0] == "--idle")
    await IdleWaitProfile.RunAsync(args[1], int.Parse(args[2]));
else if (args.Length == 2 && args[0] == "--wait-cost")
    await WaitAllocationProfile.RunAsync(int.Parse(args[1]));
else if (args.Length == 7 && args[0] == "--profile")
    await TransportProfile.RunAsync(args[1], args[2], int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6]));
else
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
