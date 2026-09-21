using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kable.Alarms;
using Kable.Localization;
using Xunit;

namespace Kable.Tests.Cases.Alarms;

public sealed class AlarmLifecycleTests
{
    [Fact]
    public void RaiseAlarm_ShouldTrackInActiveAlarms()
    {
        var manager = new AlarmManager();

        manager.RaiseAlarm(
            alarmId: "ALM_001",
            deviceId: "ROBOT_1",
            severity: AlarmSeverity.Critical,
            description: "Robot motion timeout");

        Assert.Single(manager.ActiveAlarms);
        var active = manager.ActiveAlarms.First();
        Assert.Equal("ALM_001", active.AlarmId);
        Assert.Equal("ROBOT_1", active.DeviceId);
        Assert.Equal(AlarmState.Set, active.State);
        Assert.Equal(AlarmSeverity.Critical, active.Severity);
    }

    [Fact]
    public void ClearAlarm_ShouldRemoveFromActiveAlarms()
    {
        var manager = new AlarmManager();

        manager.RaiseAlarm("ALM_002", "LP_1", AlarmSeverity.Critical, "Disconnected");
        Assert.Single(manager.ActiveAlarms);

        var cleared = manager.ClearAlarm("ALM_002", "LP_1", "Operator reset");
        Assert.True(cleared);
        Assert.Empty(manager.ActiveAlarms);
    }

    [Fact]
    public async Task AlarmStream_ShouldPublishStateChanges()
    {
        var manager = new AlarmManager();
        var received = new List<AlarmRecord>();

        manager.RaiseAlarm("ALM_003", "ALIGNER", AlarmSeverity.Warning, "Checksum err");
        manager.ClearAlarm("ALM_003", "ALIGNER", "Auto recover");

        while (manager.AlarmStream.TryRead(out var item))
        {
            received.Add(item);
        }

        Assert.Equal(2, received.Count);
        Assert.Equal(AlarmState.Set, received[0].State);
        Assert.Equal("ALM_003", received[0].AlarmId);
        Assert.Equal(AlarmState.Clear, received[1].State);
        Assert.Equal("Auto recover", received[1].Description);
    }

    [Fact]
    public void ClearAll_ShouldClearEveryActiveAlarm()
    {
        var manager = new AlarmManager();

        manager.RaiseAlarm("ALM_A", "DEV1", AlarmSeverity.Warning, "Test A");
        manager.RaiseAlarm("ALM_B", "DEV2", AlarmSeverity.Critical, "Test B");

        Assert.Equal(2, manager.ActiveAlarms.Count);

        manager.ClearAll();

        Assert.Empty(manager.ActiveAlarms);
    }
}
