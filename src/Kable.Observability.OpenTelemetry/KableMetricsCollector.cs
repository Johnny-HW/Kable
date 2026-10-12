namespace Kable.Observability.OpenTelemetry;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Kable.Observability;

public sealed class KableMetricsCollector : ICommObserver, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _packetCounter;
    private readonly Counter<long> _errorCounter;
    private readonly Histogram<double> _latencyHistogram;

    private readonly Channel<PacketTraceRecord> _periodicChannel = Channel.CreateBounded<PacketTraceRecord>(1000);
    private readonly Channel<PacketTraceRecord> _commandChannel = Channel.CreateBounded<PacketTraceRecord>(1000);
    private readonly Channel<PacketTraceRecord> _alarmChannel = Channel.CreateBounded<PacketTraceRecord>(1000);

    public ChannelReader<PacketTraceRecord> PeriodicStream => _periodicChannel.Reader;
    public ChannelReader<PacketTraceRecord> CommandStream => _commandChannel.Reader;
    public ChannelReader<PacketTraceRecord> AlarmStream => _alarmChannel.Reader;

    public KableMetricsCollector(string meterName = "Kable.Observability", string? version = "1.0.0")
    {
        _meter = new Meter(meterName, version);

        _packetCounter = _meter.CreateCounter<long>(
            "kable.packets.count",
            unit: "packets",
            description: "Total count of packet traces emitted by Kable engine.");

        _errorCounter = _meter.CreateCounter<long>(
            "kable.packets.errors",
            unit: "errors",
            description: "Total count of packet errors and timeouts recorded.");

        _latencyHistogram = _meter.CreateHistogram<double>(
            "kable.packets.latency_ms",
            unit: "ms",
            description: "Round-trip request-response latency in milliseconds.");
    }

    /// <summary>
    /// 세션 단위 통신 건전성 지표를 OpenTelemetry 메트릭으로 기록합니다.
    /// </summary>
    public void RecordHealthMetrics(string deviceId, in CommHealthMetrics metrics)
    {
        var tags = new TagList
        {
            { "device_id", deviceId },
            { "state", metrics.State.ToString() }
        };

        if (metrics.LastRttMs > 0)
        {
            _latencyHistogram.Record(metrics.LastRttMs, tags);
        }
    }

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        var tags = new TagList
        {
            { "direction", trace.Direction == PacketDirection.Tx ? "tx" : "rx" },
            { "kind", trace.Kind.ToString() },
            { "level", trace.Level.ToString() },
            { "tag", trace.Tag }
        };

        _packetCounter.Add(1, tags);

        if (trace.Level >= LogLevel.Warning)
        {
            _errorCounter.Add(1, tags);
        }

        if (trace.Latency > TimeSpan.Zero)
        {
            _latencyHistogram.Record(trace.Latency.TotalMilliseconds, tags);
        }

        switch (trace.Kind)
        {
            case TrafficKind.PeriodicTelemetry:
                _periodicChannel.Writer.TryWrite(trace);
                break;
            case TrafficKind.SpontaneousAlarm:
                _alarmChannel.Writer.TryWrite(trace);
                break;
            case TrafficKind.AperiodicCommand:
            default:
                _commandChannel.Writer.TryWrite(trace);
                break;
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
