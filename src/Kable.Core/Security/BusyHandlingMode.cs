namespace Kable.Core.Security;

/// <summary>
/// 이미 작업이 실행 중일 때 신규 요청에 대한 동시성 처리 정책을 정의합니다.
/// </summary>
public enum BusyHandlingMode
{
    /// <summary>
    /// 이미 작업이 진행 중이면 신규 요청을 즉시 거부(Fail-Fast)하고 예외 또는 Busy 상태코드를 반환합니다.
    /// </summary>
    RejectImmediately = 0,

    /// <summary>
    /// 선행 작업이 완료될 때까지 FIFO 대기열에 진입하여 순차적으로 대기합니다.
    /// </summary>
    EnqueueFifo = 1,

    /// <summary>
    /// 진행 중인 기존 작업의 CancellationToken에 취소 신호를 보내고 새 작업을 선점 실행합니다.
    /// </summary>
    PreemptCurrent = 2
}
