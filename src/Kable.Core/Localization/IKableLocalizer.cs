namespace Kable.Localization;

using System;
using System.Globalization;

/// <summary>
/// Kable 다국어 리소스 조회 인터페이스
/// </summary>
public interface IKableLocalizer
{
    /// <summary>
    /// 현재 적용된 문화권 (기본: en-US)
    /// </summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// 언어 변경 시 발생하는 이벤트
    /// </summary>
    event EventHandler<CultureInfo>? CultureChanged;

    /// <summary>
    /// 언어 문화권 설정
    /// </summary>
    void SetCulture(CultureInfo culture);

    /// <summary>
    /// 에러 코드 기반 현지화 메시지 조회
    /// </summary>
    string GetErrorMessage(KableErrorCode code, params object[] args);

    /// <summary>
    /// 키 기반 현지화 문자열 조회
    /// </summary>
    string GetString(string key, params object[] args);
}
