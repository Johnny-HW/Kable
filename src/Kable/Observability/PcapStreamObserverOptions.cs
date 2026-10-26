namespace Kable.Observability;

using System;

/// <summary>Finite recording and independently lossy live-view capacities.</summary>
public sealed class PcapStreamObserverOptions
{
    public int RecordingCapacity { get; set; } = 1000;
    public int CommandCapacity { get; set; } = 1000;
    public int PeriodicCapacity { get; set; } = 1000;
    public int AlarmCapacity { get; set; } = 1000;
    internal void Validate()
    {
        Check(RecordingCapacity, nameof(RecordingCapacity));
        Check(CommandCapacity, nameof(CommandCapacity));
        Check(PeriodicCapacity, nameof(PeriodicCapacity));
        Check(AlarmCapacity, nameof(AlarmCapacity));
    }
    private static void Check(int capacity, string name)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(name);
    }
}
