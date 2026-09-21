using System;
using FluentAssertions;
using Kable.Alarms;
using Kable.Observability;
using Xunit;

namespace Kable.Tests.Cases.Observability;

public sealed class SessionHealthMonitorTests
{
    [Fact]
    public void SessionHealthMonitor_CalculatesMetricsAndJitterAccurately()
    {
        var alarmMgr = new AlarmManager();
        var monitor = new SessionHealthMonitor(windowSize: 10, jitterWarningThresholdMs: 25.0, alarmMgr, "DEV1");

        // Stable RTT sequence: 10, 10, 10, 10, 10
        for (int i = 0; i < 5; i++) monitor.RecordRtt(10.0);

        var snap1 = monitor.GetSnapshot();
        snap1.AverageRttMs.Should().Be(10.0);
        snap1.JitterMs.Should().Be(0.0);
        snap1.HealthScore.Should().Be(100);

        // Inject jitter: 10, 50, 10, 50
        monitor.RecordRtt(50.0);
        monitor.RecordRtt(10.0);
        monitor.RecordRtt(60.0);

        var snap2 = monitor.GetSnapshot();
        snap2.JitterMs.Should().BeGreaterThan(15.0);

        // Trigger timeout
        monitor.RecordTimeout();
        var snap3 = monitor.GetSnapshot();
        snap3.TimeoutCount.Should().Be(1);
        snap3.PacketLossRate.Should().BeGreaterThan(0);
    }
}
