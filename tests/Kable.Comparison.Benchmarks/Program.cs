namespace Kable.Comparison;
public static class Program
{
    public static Task Main(string[] args) => args.Contains("--faults")
        ? Faults.FaultRunner.RunAsync(args) : Scenarios.ComparisonRunner.RunAsync(args);
}
