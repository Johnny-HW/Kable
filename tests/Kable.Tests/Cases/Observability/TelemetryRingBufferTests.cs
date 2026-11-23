namespace Kable.Tests.Cases;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Observability;
using Xunit;

public sealed class TelemetryRingBufferTests
{
    [Fact]
    public void CommObserver_OverCapacity_DropsOldestAndMaintainsLatestTelemetry()
    {
        var observer = new CommObserver(bufferCapacity: 5);

        for (int i = 0; i < 10; i++)
        {
            observer.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow,
                PacketDirection.Rx,
                TrafficKind.PeriodicTelemetry,
                "STREAM",
                ReadOnlyMemory<byte>.Empty,
                "TELEMETRY_" + i,
                TimeSpan.Zero));
        }

        int count = 0;
        int lastIndex = -1;
        while (observer.PeriodicStream.TryRead(out var record))
        {
            count++;
            var parts = record.ParsedText != null ? record.ParsedText.Split('_') : null;
            if (parts != null && parts.Length == 2 && int.TryParse(parts[1], out int idx))
            {
                lastIndex = idx;
            }
        }

        count.Should().Be(5);
        lastIndex.Should().Be(9);
    }

    [Fact]
    public void TC_OBS_101_CommObserver_MultiThreadedBursts_MaintainsZeroLossInCommands()
    {
        var observer = new CommObserver(bufferCapacity: 2000);
        int tasksCount = 50;
        int perTaskCount = 20;

        Parallel.For(0, tasksCount, taskId =>
        {
            for (int i = 0; i < perTaskCount; i++)
            {
                observer.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow,
                    PacketDirection.Tx,
                    TrafficKind.AperiodicCommand,
                    "CMD",
                    ReadOnlyMemory<byte>.Empty,
                    $"CMD_{taskId}_{i}",
                    TimeSpan.Zero));
            }
        });

        int totalRead = 0;
        var received = new HashSet<string>();
        while (observer.CommandStream.TryRead(out var rec))
        {
            totalRead++;
            if (rec.ParsedText != null)
            {
                received.Add(rec.ParsedText);
            }
        }

        totalRead.Should().Be(tasksCount * perTaskCount);
        received.Count.Should().Be(tasksCount * perTaskCount);
    }

    [Fact]
    public void TC_OBS_201_CommObserver_TelemetryBufferSaturation_MaintainsZeroLossInAlarmAndCommandStreams()
    {
        var observer = new CommObserver(bufferCapacity: 10);

        for (int i = 0; i < 100; i++)
        {
            observer.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow,
                PacketDirection.Rx,
                TrafficKind.PeriodicTelemetry,
                "SENSOR_STREAM",
                ReadOnlyMemory<byte>.Empty,
                $"DATA_{i}",
                TimeSpan.Zero,
                LogLevel.Trace));
        }

        observer.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow,
            PacketDirection.Rx,
            TrafficKind.SpontaneousAlarm,
            "CRITICAL_INTERLOCK",
            ReadOnlyMemory<byte>.Empty,
            "ALARM_FIRE_DETECTED",
            TimeSpan.Zero,
            LogLevel.Critical));

        observer.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow,
            PacketDirection.Tx,
            TrafficKind.AperiodicCommand,
            "CMD_SHUTDOWN",
            ReadOnlyMemory<byte>.Empty,
            "STOP_ALL",
            TimeSpan.Zero,
            LogLevel.Debug));

        int telemetryCount = 0;
        while (observer.PeriodicStream.TryRead(out _)) telemetryCount++;
        telemetryCount.Should().Be(10);

        observer.AlarmStream.TryRead(out var alarm).Should().BeTrue();
        alarm.ParsedText.Should().Be("ALARM_FIRE_DETECTED");
        alarm.Level.Should().Be(LogLevel.Critical);

        observer.CommandStream.TryRead(out var cmd).Should().BeTrue();
        cmd.ParsedText.Should().Be("STOP_ALL");
    }
}
