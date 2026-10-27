using BenchmarkDotNet.Running;
using Kable.SharedMemory.Benchmarks;

if (args.Length == 3 && args[0] == "--idle")
    await IdleWaitProfile.RunAsync(args[1], int.Parse(args[2]));
else if (args.Length == 2 && args[0] == "--wait-cost")
    await WaitAllocationProfile.RunAsync(int.Parse(args[1]));
else if (args.Length == 7 && args[0] is "--profile" or "--profile-gc" or "--profile-no-gc")
    await TransportProfile.RunAsync(args[1], args[2], int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6]),
        noGc: args[0] == "--profile-no-gc", prepareGc: args[0] != "--profile", warmupRequests: args[0] == "--profile" ? 100 : 5000);
else
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
