namespace Kable.Core.Security;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 에이전트/장비의 중복 실행 방지 및 Busy 상태 동시성 제어를 담당하는 가드 인터페이스입니다.
/// </summary>
public interface IConcurrencyGuard : IAsyncDisposable
{
    /// <summary>
    /// 지정된 리소스 키에 대해 실행 잠금(Busy Lock) 획득을 시도합니다.
    /// 잠금 획득 실패 시(RejectImmediately 모드 등) null을 반환합니다.
    /// </summary>
    /// <param name="resourceKey">잠금 대상 리소스/장비 고유 식별자</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>잠금을 해제할 수 있는 IAsyncDisposable 핸들 또는 획득 실패 시 null</returns>
    ValueTask<IAsyncDisposable?> TryAcquireAsync(string resourceKey, CancellationToken ct = default);

    /// <summary>
    /// 지정된 리소스 키에 대해 실행 잠금을 획득합니다.
    /// 이미 실행 중이고 RejectImmediately 모드이거나 타임아웃 발생 시 DeviceBusyException을 던집니다.
    /// </summary>
    ValueTask<IAsyncDisposable> AcquireAsync(string resourceKey, CancellationToken ct = default);

    /// <summary>
    /// 현재 특정 리소스가 작업 실행 중(Busy)인지 확인합니다.
    /// </summary>
    bool IsBusy(string resourceKey);
}
