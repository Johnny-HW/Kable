namespace Kable.Tests.Cases.Engine.Server;

using System;
using Kable.Observability;
using Xunit;

public sealed class PacketTraceRecordCompatibilityTests
{
    [Fact]
    public void LegacyConstructor_SetsConnectionIdToNull_PreservesAllFields()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var bytes = new byte[] { 0x01, 0x02, 0x03 };
        var latency = TimeSpan.FromMilliseconds(5);

        // Act - Call original constructor without connectionId
        var record = new PacketTraceRecord(
            timestampUtc: now,
            direction: PacketDirection.Rx,
            kind: TrafficKind.AperiodicCommand,
            tag: "TEST_TAG",
            rawBytes: bytes,
            parsedText: "REQ_A",
            latency: latency,
            level: LogLevel.Warning,
            deviceId: "DEV-01",
            sequenceNo: 12345L);

        // Assert
        Assert.Null(record.ConnectionId);
        Assert.Equal(now, record.TimestampUtc);
        Assert.Equal(PacketDirection.Rx, record.Direction);
        Assert.Equal(TrafficKind.AperiodicCommand, record.Kind);
        Assert.Equal("TEST_TAG", record.Tag);
        Assert.Equal(bytes, record.RawBytes.ToArray());
        Assert.Equal("REQ_A", record.ParsedText);
        Assert.Equal(latency, record.Latency);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal("DEV-01", record.DeviceId);
        Assert.Equal(12345L, record.SequenceNo);
    }

    [Fact]
    public void NewConstructor_WithConnectionId_PreservesConnectionIdAndAllFields()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var bytes = new byte[] { 0xAA, 0xBB };
        var latency = TimeSpan.FromMilliseconds(12);

        // Act - Call new constructor with explicit connectionId
        var record = new PacketTraceRecord(
            connectionId: "conn-tcp-998",
            timestampUtc: now,
            direction: PacketDirection.Tx,
            kind: TrafficKind.PeriodicTelemetry,
            tag: "STATUS_TX",
            rawBytes: bytes,
            parsedText: "STAT_OK",
            latency: latency,
            level: LogLevel.Information,
            deviceId: "ROBOT-A",
            sequenceNo: 54321L);

        // Assert
        Assert.Equal("conn-tcp-998", record.ConnectionId);
        Assert.Equal(now, record.TimestampUtc);
        Assert.Equal(PacketDirection.Tx, record.Direction);
        Assert.Equal(TrafficKind.PeriodicTelemetry, record.Kind);
        Assert.Equal("STATUS_TX", record.Tag);
        Assert.Equal(bytes, record.RawBytes.ToArray());
        Assert.Equal("STAT_OK", record.ParsedText);
        Assert.Equal(latency, record.Latency);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("ROBOT-A", record.DeviceId);
        Assert.Equal(54321L, record.SequenceNo);
    }
}
