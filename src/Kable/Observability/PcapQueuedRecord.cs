namespace Kable.Observability;

using System;

internal readonly struct PcapQueuedRecord
{
    public byte[] Payload { get; }
    public int OriginalLength { get; }
    public DateTime TimestampUtc { get; }
    public PcapQueuedRecord(byte[] payload, int originalLength, DateTime timestampUtc)
    {
        Payload = payload;
        OriginalLength = originalLength;
        TimestampUtc = timestampUtc;
    }
}
