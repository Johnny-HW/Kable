namespace Kable.Core.Security;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 외부 프로세스 및 스크립트 실행(LAUNCH) 요청에 대해 허용 목록 및 보안 검증을 수행하는 가드 인터페이스입니다.
/// </summary>
public interface ILaunchGuard
{
    /// <summary>
    /// 실행 요청된 파일 경로와 인자를 검증하고, 디렉터리 트래버설을 방지한 정규화된 절대 경로를 반환합니다.
    /// 허용되지 않은 파일, 위험 스크립트, 위변조된 해시 또는 쉘 인젝션 위험이 감지되면 SecurityValidationException을 던집니다.
    /// </summary>
    /// <param name="executablePath">실행할 파일 경로</param>
    /// <param name="arguments">명령줄 인자</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>검증된 안전한 절대 경로</returns>
    ValueTask<string> ValidateAndNormalizeAsync(string executablePath, string? arguments = null, CancellationToken ct = default);
}
