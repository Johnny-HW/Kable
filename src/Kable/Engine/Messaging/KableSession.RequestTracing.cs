namespace Kable.Engine;

using System;
using Kable.Observability;

public sealed partial class KableSession<TMessage>
{
    private void TraceRequestPacket(TMessage message, PacketDirection direction,
        TrafficKind kind, string tag, TimeSpan latency)
    {
        if (_observer == null) return;
        ReadOnlyMemory<byte> raw = message is IRawByteProvider provider
            ? provider.ToRawBytes() : ReadOnlyMemory<byte>.Empty;
        _observer.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow, direction, kind, tag, raw, message?.ToString(), latency,
            LogLevel.Information, DeviceId));
    }
}
