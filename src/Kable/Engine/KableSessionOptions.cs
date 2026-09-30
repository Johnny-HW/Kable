namespace Kable.Engine;

using System;
using System.Threading.Channels;

/// <summary>
/// Kable 세션의 버퍼링 및 수신 큐 초과 처리 정책을 정의하는 옵션입니다.
/// </summary>
/// <typeparam name="TMessage">프로토콜 메시지 타입</typeparam>
public sealed class KableSessionOptions<TMessage>
{
    /// <summary>
    /// 수신 스트림 큐의 최대 용량 (기본값: 10,000). 느린 소비자 환경에서 메모리 상한을 보장합니다.
    /// </summary>
    public int InboundQueueCapacity { get; set; } = 10000;

    /// <summary>
    /// 메시지가 긴급 알람/이벤트인지 판별하는 델리게이트.
    /// 알람으로 판별된 메시지는 텔레메트리 버퍼가 가득 차더라도 절대로 Drop되지 않으며 최우선 보존됩니다.
    /// </summary>
    public Func<TMessage, bool>? IsAlarmMessage { get; set; }

    /// <summary>
    /// 수신 버퍼 가득 참 시 일반 텔레메트리 처리 정책 (기본값: Wait - 백프레셔 적용).
    /// 느린 소비자 환경에서 최신 데이터 위주 유지를 원할 경우 DropOldest를 선택할 수 있습니다.
    /// </summary>
    public BoundedChannelFullMode TelemetryOverflowMode { get; set; } = BoundedChannelFullMode.Wait;

    /// <summary>
    /// 알람 전용 큐의 최대 용량 (기본값: 1,000). 소비자 정지 또는 알람 폭주 시 무제한 메모리 증가를 방지합니다.
    /// </summary>
    public int AlarmQueueCapacity { get; set; } = 1000;

    /// <summary>
    /// 알람 버퍼 포화 시 처리 정책 (기본값: ThrowAndAbort - 조용한 유실을 방지하고 AlarmBufferOverflowException으로 세션을 안전하게 페일패스트 중단).
    /// </summary>
    public AlarmOverflowMode AlarmOverflowMode { get; set; } = AlarmOverflowMode.ThrowAndAbort;

    /// <summary>
    /// AlarmOverflowMode.SpoolToStorage 모드일 때 비동기 외부 저장소 이관 핸들러에 적용할 제한시간 (기본값: 3초).
    /// 저장 작업이 이 제한시간을 초과하거나 세션 종료 시 토큰이 취소되어 워커 누수를 차단합니다.
    /// </summary>
    public TimeSpan AlarmSpoolTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// AlarmOverflowMode.SpoolToStorage 모드일 때 세션 종료 시 잔여 알람 전체 배출에 허용할 총 제한시간 (기본값: 1.5초).
    /// 개별 메시지마다 타이머를 생성하지 않고 전체 배출에 단 하나의 제한시간을 적용하여 종료 시간을 엄격히 제어합니다.
    /// </summary>
    public TimeSpan AlarmSpoolDrainTimeout { get; set; } = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// AlarmOverflowMode.SpoolToStorage 모드일 때 세션 종료 시 스풀 큐에 남아있는 미저장 알람 처리 정책.
    /// </summary>
    public UnspooledAlarmDrainPolicy UnspooledAlarmDrainPolicy { get; set; } = UnspooledAlarmDrainPolicy.DrainWithinTimeout;

    /// <summary>
    /// AlarmOverflowMode.SpoolToStorage 모드일 때 호출되는 비동기 외부 저장소 이관 핸들러 (메시지, 취소 토큰).
    /// 핸들러는 반드시 전달된 CancellationToken을 준수(비동기 I/O 전달 또는 ThrowIfCancellationRequested)해야 합니다.
    /// 반환값이 true이면 성공적으로 이관된 것으로 간주하며, false이거나 취소/실패 시 세션을 중단합니다.
    /// </summary>
    public Func<TMessage, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<bool>>? OnAlarmOverflowAsync { get; set; }

    /// <summary>
    /// AlarmOverflowMode.SpoolToStorage 모드일 때 사용하는 비동기 알람 스풀 큐의 최대 용량 (기본값: 1,000).
    /// 디스패치 루프는 큐 등록만 수행하며, 스풀 큐 포화 또는 저장소 실패 시 명시적으로 세션을 중단합니다.
    /// </summary>
    public int AlarmSpoolQueueCapacity { get; set; } = 1000;
}

/// <summary>
/// 알람 스풀 워커의 저장 처리 결과 집계 리포트입니다.
/// </summary>
public sealed class AlarmSpoolSummary
{
    /// <summary>
    /// 성공적으로 외부 저장소에 영속화 완료된 알람 건수
    /// </summary>
    public long StoredCount { get; }

    /// <summary>
    /// 저장 중 취소 또는 타임아웃되어 실제 저장 여부가 불분명한 알람 건수 (중복 방지 및 추적용)
    /// </summary>
    public long InDoubtCount { get; }

    /// <summary>
    /// 저장 실패(false 반환, 예외) 또는 배출 제한시간 초과로 인해 처리되지 못한 미처리 알람 건수
    /// </summary>
    public long UnprocessedCount { get; }

    public AlarmSpoolSummary(long storedCount, long inDoubtCount, long unprocessedCount)
    {
        StoredCount = storedCount;
        InDoubtCount = inDoubtCount;
        UnprocessedCount = unprocessedCount;
    }

    public override string ToString() =>
        $"Stored={StoredCount}, InDoubt={InDoubtCount}, Unprocessed={UnprocessedCount}";
}

/// <summary>
/// 세션 종료 시 스풀 큐에 남아있는 미저장 알람의 배출 정책.
/// </summary>
public enum UnspooledAlarmDrainPolicy
{
    /// <summary>
    /// 세션 종료 시 남아있는 알람을 지정된 제한시간 내에 최대한 외부 저장소로 배출 시도.
    /// </summary>
    DrainWithinTimeout = 0,

    /// <summary>
    /// 세션 종료 시 즉시 중단하고 잔여 큐를 비움.
    /// </summary>
    AbortImmediately = 1
}

/// <summary>
/// 알람 큐 버퍼 포화 시의 처리 계약을 정의합니다.
/// </summary>
public enum AlarmOverflowMode
{
    /// <summary>
    /// 조용한 유실 방지(No Silent Drop): AlarmBufferOverflowException을 발생시키고 세션을 안전하게 페일패스트 중단합니다.
    /// </summary>
    ThrowAndAbort = 0,

    /// <summary>
    /// 외부 영속 저장소(OnAlarmOverflowAsync)로 알람을 이관하여 메모리 외부로 스풀링합니다.
    /// </summary>
    SpoolToStorage = 1,

    /// <summary>
    /// 가장 오래된 알람을 드롭하고 경고 로그를 기록합니다 (명시적인 유실 허용 모드).
    /// </summary>
    DropOldestWithWarning = 2
}
