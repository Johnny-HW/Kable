namespace Kable.Core.Security;

/// <summary>
/// 수신된 요청의 인증 토큰(Bearer, API Key 등)을 검증하는 인터페이스입니다.
/// </summary>
public interface ITokenAuthValidator
{
    /// <summary>
    /// 순수 토큰 문자열의 유효성을 검증합니다.
    /// </summary>
    bool ValidateToken(string? token);

    /// <summary>
    /// 헤더/메타데이터 원본 값(예: "Bearer <token>")에서 토큰을 추출하고 유효성을 검증합니다.
    /// </summary>
    bool ValidateHeader(string? rawHeaderValue);
}
