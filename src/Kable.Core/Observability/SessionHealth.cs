namespace Kable.Observability;

using System;

/// <summary>
/// 통신 세션의 건강도 및 지연 품질 메트릭 스냅샷
/// </summary>
public readonly struct SessionHealth
{
    public double AverageRttMs { get; }
    public double MinRttMs { get; }
    public double MaxRttMs { get; }
    public double JitterMs { get; }
    public long TotalPackets { get; }
    public long TimeoutCount { get; }
    public double PacketLossRate { get; }
    public int HealthScore { get; } // 0 (치명) ~ 100 (최상)
    public DateTime TimestampUtc { get; }

    public SessionHealth(
        double averageRttMs,
        double minRttMs,
        double maxRttMs,
        double jitterMs,
        long totalPackets,
        long timeoutCount,
        double packetLossRate,
        int healthScore,
        DateTime? timestampUtc = null)
    {
        AverageRttMs = averageRttMs;
        MinRttMs = minRttMs;
        MaxRttMs = maxRttMs;
        JitterMs = jitterMs;
        TotalPackets = totalPackets;
        TimeoutCount = timeoutCount;
        PacketLossRate = packetLossRate;
        HealthScore = healthScore < 0 ? 0 : (healthScore > 100 ? 100 : healthScore);
        TimestampUtc = timestampUtc ?? DateTime.UtcNow;
    }

    public override string ToString() =>
        $"[Health {HealthScore}/100] RTT: {AverageRttMs:F1}ms (Min:{MinRttMs:F1}, Max:{MaxRttMs:F1}), Jitter: {JitterMs:F1}ms, Loss: {PacketLossRate:P1}";
}
