namespace Kable.Observability;

using System;

/// <summary>
/// 하드웨어 또는 통신 장치의 물리적/논리적 연결 상태
/// </summary>
public enum DeviceConnectionState
{
    /// <summary>
    /// 연결이 해제되었거나 초기화되지 않은 상태
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// 물리/소켓 연결을 시도 중인 상태
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// 실물 하드웨어와 정상적으로 연결되어 통신이 가능한 상태
    /// </summary>
    Connected = 2,

    /// <summary>
    /// 실물 하드웨어 없이 모의 시뮬레이터(Virtual/Mock) 모드로 동작 중인 상태
    /// </summary>
    Simulated = 3,

    /// <summary>
    /// 통신 타임아웃, 심각한 프로토콜 오류 또는 연결 결함(Fault)이 발생한 상태
    /// </summary>
    Faulted = 4
}

/// <summary>
/// 통신 건전성 및 런타임 품질 지표 스냅샷 (Zero-Alloc 불변 구조체)
/// </summary>
public readonly record struct CommHealthMetrics
{
    /// <summary>
    /// 현재 장비 연결 상태 (Connected / Disconnected / Simulated / Faulted 등)
    /// </summary>
    public DeviceConnectionState State { get; init; }

    /// <summary>
    /// 가장 최근 측정된 통신 왕복 시간 (밀리초)
    /// </summary>
    public double LastRttMs { get; init; }

    /// <summary>
    /// 지수 이동 평균(EMA) 통신 왕복 시간 (밀리초)
    /// </summary>
    public double AvgRttMs { get; init; }

    /// <summary>
    /// 상위 99% 레이턴시 (밀리초, 심한 지터/지연 감지용)
    /// </summary>
    public double P99RttMs { get; init; }

    /// <summary>
    /// 총 송신(Tx) 성공 패킷/메시지 수
    /// </summary>
    public long TotalTxPackets { get; init; }

    /// <summary>
    /// 총 수신(Rx) 성공 패킷/메시지 수
    /// </summary>
    public long TotalRxPackets { get; init; }

    /// <summary>
    /// 통신 에러 발생 수 (타임아웃, NAK, CRC/체크섬 불일치, 전송 실패 등)
    /// </summary>
    public long ErrorPackets { get; init; }

    /// <summary>
    /// 전체 통신 대비 에러율 백분율 (0.0% ~ 100.0%)
    /// </summary>
    public double ErrorRatePercentage
    {
        get
        {
            long total = TotalTxPackets + TotalRxPackets + ErrorPackets;
            if (total <= 0) return 0.0;
            return (double)ErrorPackets / total * 100.0;
        }
    }

    /// <summary>
    /// 마지막 정상 패킷 송수신 시각 (UTC)
    /// </summary>
    public DateTime LastActiveUtc { get; init; }

    public CommHealthMetrics(
        DeviceConnectionState state,
        double lastRttMs,
        double avgRttMs,
        double p99RttMs,
        long totalTxPackets,
        long totalRxPackets,
        long errorPackets,
        DateTime lastActiveUtc)
    {
        State = state;
        LastRttMs = lastRttMs;
        AvgRttMs = avgRttMs;
        P99RttMs = p99RttMs;
        TotalTxPackets = totalTxPackets;
        TotalRxPackets = totalRxPackets;
        ErrorPackets = errorPackets;
        LastActiveUtc = lastActiveUtc;
    }

    /// <summary>
    /// 초기 기본값 (연결 해제 상태)
    /// </summary>
    public static CommHealthMetrics Empty => new(
        DeviceConnectionState.Disconnected,
        0.0, 0.0, 0.0, 0, 0, 0, DateTime.MinValue);
}
