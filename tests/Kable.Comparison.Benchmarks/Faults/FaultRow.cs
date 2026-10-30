namespace Kable.Comparison.Faults;

public sealed record FaultRow(string Library, string Mode, string Scenario, int Trial,
    string Outcome, string Detail, double LatencyMs, double OvershootMs,
    int SentCommands, string ErrorType, double CleanupMs = 0)
{
    public bool Passed => Outcome is "Pass" or "SafeRejected" or "CorrectReply";
}

public sealed record ResourceRow(string Library, string Mode, int Cycles,
    long ManagedBytes, long PrivateBytes, int Handles, int Threads);
