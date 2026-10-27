namespace Kable.SharedMemory.WaitDiagnostics;

using System.Diagnostics;

// These fields are written only by an exported diagnostic copy of RegisteredEventWait.
internal sealed class WaitPhaseProbe
{
    internal static WaitPhaseProbe? Current = null;
    internal long Entered = 0;
    internal long RegisterStart = 0;
    internal long RegisterEnd = 0;
    internal long CancellationRegistered = 0;
    internal long Signaled = 0;
    internal long Callback = 0;
    internal long Completing = 0;
    internal long Resumed = 0;
    internal long UnregisterStart = 0;
    internal long UnregisterEnd = 0;
    internal static long Now() => Stopwatch.GetTimestamp();
}
