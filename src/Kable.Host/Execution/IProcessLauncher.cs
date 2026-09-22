namespace Kable.Host.Execution;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 외부 프로세스를 안전하게 실행하고 관리하는 런처 인터페이스입니다.
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    /// 보안 검증 및 동시성 제어를 거쳐 안전하게 외부 프로세스를 구동합니다.
    /// </summary>
    /// <param name="command">실행 명령 명세</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>구동된 Process 인스턴스</returns>
    ValueTask<Process> LaunchAsync(LaunchCommand command, CancellationToken ct = default);
}
