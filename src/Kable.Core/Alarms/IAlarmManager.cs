namespace Kable.Alarms;

using System;
using System.Collections.Generic;
using System.Threading.Channels;

/// <summary>
/// 알람 라이프사이클(발생/해제) 관리자 인터페이스
/// </summary>
public interface IAlarmManager
{
    /// <summary>
    /// 현재 활성(Active) 상태인 알람 목록 조회 (Lock-free 스냅샷)
    /// </summary>
    IReadOnlyCollection<AlarmRecord> ActiveAlarms { get; }

    /// <summary>
    /// 실시간 알람 발생/해제 이벤트 스트림 (상위 MES/UI 통지용)
    /// </summary>
    ChannelReader<AlarmRecord> AlarmStream { get; }

    /// <summary>
    /// 알람 발생 (Set)
    /// </summary>
    void RaiseAlarm(string alarmId, string deviceId, AlarmSeverity severity, string description);

    /// <summary>
    /// 알람 해제 (Clear)
    /// </summary>
    bool ClearAlarm(string alarmId, string deviceId, string? clearReason = null);

    /// <summary>
    /// 특정 장치의 모든 알람 일괄 해제
    /// </summary>
    void ClearAll(string? deviceId = null);
}
