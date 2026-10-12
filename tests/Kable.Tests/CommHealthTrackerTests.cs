namespace Kable.Tests.Observability;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Core.Metrics;
using Kable.Observability;
using Xunit;

public class CommHealthTrackerTests
{
    [Fact]
    public void InitialState_ShouldBeDisconnected_AndEmptyMetrics()
    {
        var tracker = new CommHealthTracker();
        var snapshot = tracker.GetMetricsSnapshot();

        snapshot.State.Should().Be(DeviceConnectionState.Disconnected);
        snapshot.TotalTxPackets.Should().Be(0);
        snapshot.TotalRxPackets.Should().Be(0);
        snapshot.ErrorPackets.Should().Be(0);
        snapshot.ErrorRatePercentage.Should().Be(0.0);
        snapshot.LastRttMs.Should().Be(0.0);
    }

    [Fact]
    public void RecordTxRxAndErrors_ShouldCalculateAccurateErrorRate()
    {
        var tracker = new CommHealthTracker();
        tracker.SetState(DeviceConnectionState.Connected);

        tracker.RecordTx(80);
        tracker.RecordRx(100);
        tracker.RecordError(20);

        var snapshot = tracker.GetMetricsSnapshot();

        snapshot.State.Should().Be(DeviceConnectionState.Connected);
        snapshot.TotalTxPackets.Should().Be(80);
        snapshot.TotalRxPackets.Should().Be(100);
        snapshot.ErrorPackets.Should().Be(20);

        // Total = 80 + 100 + 20 = 200. ErrorRate = 20 / 200 = 10.0%
        snapshot.ErrorRatePercentage.Should().Be(10.0);
    }

    [Fact]
    public void RecordRtt_ShouldUpdateLastRtt_AndEmaAverage()
    {
        var tracker = new CommHealthTracker();
        tracker.SetState(DeviceConnectionState.Connected);

        tracker.RecordRtt(TimeSpan.FromMilliseconds(10));
        var s1 = tracker.GetMetricsSnapshot();
        s1.LastRttMs.Should().BeInRange(9.5, 10.5);
        s1.AvgRttMs.Should().BeInRange(9.5, 10.5);

        // Record a second RTT of 20ms. EMA (alpha=0.2): 10 * 0.8 + 20 * 0.2 = 12ms
        tracker.RecordRtt(TimeSpan.FromMilliseconds(20));
        var s2 = tracker.GetMetricsSnapshot();
        s2.LastRttMs.Should().BeInRange(19.5, 20.5);
        s2.AvgRttMs.Should().BeInRange(11.5, 12.5);
    }

    [Fact]
    public void Reset_ShouldClearAllCounters_AndMaintainState()
    {
        var tracker = new CommHealthTracker();
        tracker.SetState(DeviceConnectionState.Simulated);
        tracker.RecordTx(50);
        tracker.RecordRx(50);
        tracker.RecordError(5);
        tracker.RecordRtt(TimeSpan.FromMilliseconds(15));

        tracker.Reset();
        var snapshot = tracker.GetMetricsSnapshot();

        snapshot.State.Should().Be(DeviceConnectionState.Simulated);
        snapshot.TotalTxPackets.Should().Be(0);
        snapshot.TotalRxPackets.Should().Be(0);
        snapshot.ErrorPackets.Should().Be(0);
        snapshot.ErrorRatePercentage.Should().Be(0.0);
        snapshot.LastRttMs.Should().Be(0.0);
    }
}
