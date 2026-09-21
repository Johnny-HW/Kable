namespace Kable.Alarms;

using System;

/// <summary>
/// 알람 심각도 (반도체/산업 표준)
/// </summary>
public enum AlarmSeverity
{
    Info = 0,
    Warning = 1,  // 경고: 설비 가동 유지
    Critical = 2, // 위험: 특정 모듈 인터록
    Fatal = 3     // 치명: 설비 전체 E-STOP
}

/// <summary>
/// 알람 상태 (발생 vs 해제)
/// </summary>
public enum AlarmState
{
    Set = 1,   // 알람 발생
    Clear = 0  // 알람 해제 (정상 복구)
}

/// <summary>
/// 상위 MES/SECS-GEM 연동을 위한 알람 표준 레코드
/// </summary>
public readonly struct AlarmRecord
{
    public string AlarmId { get; }
    public string DeviceId { get; }
    public AlarmState State { get; }
    public AlarmSeverity Severity { get; }
    public string Description { get; }
    public DateTime TimestampUtc { get; }

    public AlarmRecord(
        string alarmId,
        string deviceId,
        AlarmState state,
        AlarmSeverity severity,
        string description,
        DateTime? timestampUtc = null)
    {
        AlarmId = alarmId ?? throw new ArgumentNullException(nameof(alarmId));
        DeviceId = deviceId ?? "DEFAULT";
        State = state;
        Severity = severity;
        Description = description ?? string.Empty;
        TimestampUtc = timestampUtc ?? DateTime.UtcNow;
    }

    public override string ToString() =>
        $"[{TimestampUtc:HH:mm:ss.fff}] [{DeviceId}] ALARM_{State.ToString().ToUpperInvariant()} [{AlarmId}] ({Severity}): {Description}";
}
