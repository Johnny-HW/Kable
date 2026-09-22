namespace Kable.Core.Security;

using System;

/// <summary>
/// 에이전트/장비 세션의 중복 실행 방지 및 Busy 락 동작 정책을 설정하는 모델입니다.
/// </summary>
public record ConcurrencyOptions
{
    /// <summary>
    /// 동시 실행 방지 활성화 여부
    /// </summary>
    public bool PreventDuplicateExecution { get; init; } = true;

    /// <summary>
    /// 동시 실행 충돌 시 처리 모드 (기본: RejectImmediately)
    /// </summary>
    public BusyHandlingMode Mode { get; init; } = BusyHandlingMode.RejectImmediately;

    /// <summary>
    /// EnqueueFifo 모드 시 허용할 최대 대기 큐 크기 (초과 시 즉시 거부)
    /// </summary>
    public int MaxQueueCapacity { get; init; } = 32;

    /// <summary>
    /// 락 획득 대기 타임아웃 (밀리초)
    /// </summary>
    public TimeSpan LockAcquisitionTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
