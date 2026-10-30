namespace Kable.Observability;

using System;
using System.Threading.Channels;

/// <summary>Preserves session identity without changing the observer exception contract.</summary>
internal sealed class SessionCommObserver(ICommObserver inner, string deviceId) : ICommObserver
{
    public ChannelReader<PacketTraceRecord> PeriodicStream => inner.PeriodicStream;
    public ChannelReader<PacketTraceRecord> CommandStream => inner.CommandStream;
    public ChannelReader<PacketTraceRecord> AlarmStream => inner.AlarmStream;

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        var identified = trace.ConnectionId == null
            ? new PacketTraceRecord(trace.TimestampUtc, trace.Direction, trace.Kind, trace.Tag,
                trace.RawBytes, trace.ParsedText, trace.Latency, trace.Level, deviceId, trace.SequenceNo)
            : new PacketTraceRecord(trace.ConnectionId, trace.TimestampUtc, trace.Direction, trace.Kind, trace.Tag,
                trace.RawBytes, trace.ParsedText, trace.Latency, trace.Level, deviceId, trace.SequenceNo);
        inner.OnPacketTrace(identified);
    }
}
