namespace Kable.Tests.Cases.Observability;

using Kable.Alarms;
using Kable.Observability;
using Xunit;

public sealed class HealthMonitorAllocationTests
{
    [Fact]
    public void MonitoringWithoutAlarmDoesNotAllocateSnapshotForEverySample()
    {
        var monitor = new SessionHealthMonitor(50, 1000000, new AlarmManager());
        for (int i = 0; i < 100; i++) monitor.RecordRtt(i % 10);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) monitor.RecordRtt(i % 10);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(monitor.GetSnapshot().JitterMs > 0);
    }
}
