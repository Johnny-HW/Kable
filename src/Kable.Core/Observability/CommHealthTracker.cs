namespace Kable.Observability;

using System;
using System.Diagnostics;
using System.Threading;
using Kable.Core.Metrics;

/// <summary>
/// 런타임 제로 알로케이션(Zero-GC) 통신 건전성 및 메트릭스 트래커.
/// 원자적(Interlocked) 연산과 지수이동평균(EMA)을 사용하여 수십만 ops/sec 환경에서도
/// 락 없이 초고속으로 RTT, 패킷 수 및 에러율을 집계합니다.
/// </summary>
public sealed class CommHealthTracker
{
    private readonly ZeroAllocLatencyCollector _latencyCollector;
    private int _state;
    private long _totalTxPackets;
    private long _totalRxPackets;
    private long _errorPackets;
    private long _lastActiveTicks;
    private long _lastRttTicks;
    private long _avgRttTicks; // Micro-ticks for fixed-point EMA calculation

    // EMA smoothing factor: alpha = 0.2 (20% weight to latest measurement)
    private const double EmaAlpha = 0.2;

    public CommHealthTracker(int latencyCapacity = 8192)
    {
        _latencyCollector = new ZeroAllocLatencyCollector(latencyCapacity);
        _state = (int)DeviceConnectionState.Disconnected;
        _lastActiveTicks = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// 현재 연결 상태를 변경합니다.
    /// </summary>
    public void SetState(DeviceConnectionState state)
    {
        Volatile.Write(ref _state, (int)state);
        UpdateActiveTimestamp();
    }

    /// <summary>
    /// 송신(Tx) 성공 패킷을 기록합니다.
    /// </summary>
    public void RecordTx(int count = 1)
    {
        Interlocked.Add(ref _totalTxPackets, count);
        UpdateActiveTimestamp();
    }

    /// <summary>
    /// 수신(Rx) 성공 패킷을 기록합니다.
    /// </summary>
    public void RecordRx(int count = 1)
    {
        Interlocked.Add(ref _totalRxPackets, count);
        UpdateActiveTimestamp();
    }

    /// <summary>
    /// 통신 오류(타임아웃, CRC 불일치, NAK 등)를 기록합니다.
    /// </summary>
    public void RecordError(int count = 1)
    {
        Interlocked.Add(ref _errorPackets, count);
        UpdateActiveTimestamp();
    }

    /// <summary>
    /// 통신 왕복 시간(RTT)을 기록하고 지수이동평균(EMA) 및 P99 히스토그램을 갱신합니다.
    /// </summary>
    /// <param name="elapsedTicks">Stopwatch 틱 단위 경과 시간</param>
    public void RecordRttTicks(long elapsedTicks)
    {
        if (elapsedTicks < 0) elapsedTicks = 0;

        Volatile.Write(ref _lastRttTicks, elapsedTicks);
        _latencyCollector.RecordTicks(elapsedTicks);

        // EMA 계산 (Thread-safe CAS loop)
        long currentAvg = Volatile.Read(ref _avgRttTicks);
        while (true)
        {
            long newAvg = currentAvg == 0
                ? elapsedTicks
                : (long)(currentAvg * (1.0 - EmaAlpha) + elapsedTicks * EmaAlpha);

            if (Interlocked.CompareExchange(ref _avgRttTicks, newAvg, currentAvg) == currentAvg)
            {
                break;
            }
            currentAvg = Volatile.Read(ref _avgRttTicks);
        }

        UpdateActiveTimestamp();
    }

    /// <summary>
    /// TimeSpan 단위의 RTT를 기록합니다.
    /// </summary>
    public void RecordRtt(TimeSpan latency)
    {
        long ticks = (long)(latency.TotalSeconds * Stopwatch.Frequency);
        RecordRttTicks(ticks);
    }

    /// <summary>
    /// 현재 시점의 통신 건전성 지표 불변 스냅샷을 반환합니다.
    /// </summary>
    public CommHealthMetrics GetMetricsSnapshot()
    {
        var state = (DeviceConnectionState)Volatile.Read(ref _state);
        long tx = Volatile.Read(ref _totalTxPackets);
        long rx = Volatile.Read(ref _totalRxPackets);
        long err = Volatile.Read(ref _errorPackets);
        long lastRtt = Volatile.Read(ref _lastRttTicks);
        long avgRtt = Volatile.Read(ref _avgRttTicks);
        long lastActive = Volatile.Read(ref _lastActiveTicks);

        double tickToMs = 1000.0 / Stopwatch.Frequency;
        double lastRttMs = lastRtt * tickToMs;
        double avgRttMs = avgRtt * tickToMs;

        var summary = _latencyCollector.ComputeSummary();
        double p99RttMs = summary.P99Ns / 1_000_000.0;

        DateTime lastActiveUtc = DateTime.UtcNow - TimeSpan.FromSeconds((double)(Stopwatch.GetTimestamp() - lastActive) / Stopwatch.Frequency);

        return new CommHealthMetrics(
            state,
            Math.Round(lastRttMs, 3),
            Math.Round(avgRttMs, 3),
            Math.Round(p99RttMs, 3),
            tx,
            rx,
            err,
            lastActiveUtc);
    }

    /// <summary>
    /// 카운터 및 지표를 초기화합니다.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _totalTxPackets, 0);
        Interlocked.Exchange(ref _totalRxPackets, 0);
        Interlocked.Exchange(ref _errorPackets, 0);
        Interlocked.Exchange(ref _lastRttTicks, 0);
        Interlocked.Exchange(ref _avgRttTicks, 0);
        _latencyCollector.Reset();
        UpdateActiveTimestamp();
    }

    private void UpdateActiveTimestamp()
    {
        Volatile.Write(ref _lastActiveTicks, Stopwatch.GetTimestamp());
    }
}
