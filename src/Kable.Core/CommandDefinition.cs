namespace Kable.Protocol;

using System;

/// <summary>
/// 명령어의 운영 실행 모드 (상시 자동 스트리밍 vs 수시 단발성 제어)
/// </summary>
public enum CommandExecutionMode
{
    /// <summary>
    /// 수시(Aperiodic) 명령: 사용자/시나리오가 필요할 때 수동 또는 단발성으로 호출 (Request/Response)
    /// </summary>
    Aperiodic,

    /// <summary>
    /// 상시(Periodic) 명령: 엔진/장비가 지정된 주기(IntervalMs)마다 주기적으로 자동 송수신 (Telemetry Streaming/Polling)
    /// </summary>
    Periodic
}

/// <summary>
/// 명령어 기능 본체 정의 (프로토콜 페이로드 포맷, 파라미터 스펙, 단위)
/// 동일한 기능 정의 하나로 상시(Periodic) 또는 수시(Aperiodic) 모드에 모두 유연하게 적용됩니다.
/// </summary>
public readonly record struct CommandDefinition(
    string Id,
    string Name,
    string RequestPayload = "",
    string ResponsePayload = "",
    string Unit = "",
    double DefaultValue = 0.0,
    double MinValue = 0.0,
    double MaxValue = 100.0,
    double Step = 1.0,
    double ScaleFactor = 1.0
);

/// <summary>
/// 상시 또는 수시 스케줄링 운영 속성이 결합된 명령어 항목
/// 동일한 기능(CommandDefinition)을 유지하면서 상시 리스트와 수시 리스트 간 상호 전환이 가능합니다.
/// </summary>
public readonly record struct ScheduledCommandItem(
    CommandDefinition Definition,
    CommandExecutionMode Mode = CommandExecutionMode.Aperiodic,
    int IntervalMs = 500,
    bool IsEnabled = true
)
{
    /// <summary>
    /// 상시(Periodic) 텔레메트리 스트리밍 항목으로 전환
    /// </summary>
    public ScheduledCommandItem ToPeriodic(int intervalMs = 500) =>
        this with { Mode = CommandExecutionMode.Periodic, IntervalMs = Math.Max(20, intervalMs) };

    /// <summary>
    /// 수시(Aperiodic) 파라미터/제어 명령 항목으로 전환
    /// </summary>
    public ScheduledCommandItem ToAperiodic() =>
        this with { Mode = CommandExecutionMode.Aperiodic };

    /// <summary>
    /// 활성화/비활성화 토글
    /// </summary>
    public ScheduledCommandItem SetEnabled(bool enabled) =>
        this with { IsEnabled = enabled };
}
