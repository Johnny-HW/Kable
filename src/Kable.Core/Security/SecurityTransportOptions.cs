namespace Kable.Core.Security;

using System;
using System.Collections.Generic;

/// <summary>
/// 전송 계층의 보안(TLS, mTLS, 토큰 인증, 로컬 IPC 접근 제어)을 구성하는 옵션 모델입니다.
/// </summary>
public record SecurityTransportOptions
{
    /// <summary>
    /// TLS 암호화 활성화 여부
    /// </summary>
    public bool EnableTls { get; init; } = false;

    /// <summary>
    /// 서버 인증서 파일 경로 (.pfx 또는 .crt)
    /// </summary>
    public string? CertificatePath { get; init; }

    /// <summary>
    /// 서버 인증서 암호
    /// </summary>
    public string? CertificatePassword { get; init; }

    /// <summary>
    /// 클라이언트 상호 인증(mTLS) 요구 여부
    /// </summary>
    public bool RequireClientCertificate { get; init; } = false;

    /// <summary>
    /// 클라이언트 신뢰 CA 인증서 경로 (mTLS 검증용)
    /// </summary>
    public string? ClientRootCertificatePath { get; init; }

    /// <summary>
    /// 토큰 기반 인증 요구 여부 (gRPC Header / Metadata / HTTP Bearer)
    /// </summary>
    public bool RequireTokenAuthentication { get; init; } = false;

    /// <summary>
    /// 메타데이터/헤더에 포함될 인증 토큰 키 (기본: "authorization")
    /// </summary>
    public string HeaderKey { get; init; } = "authorization";

    /// <summary>
    /// 사전 정의된 유효한 정적 토큰 목록 (Bearer 접두어 포함 또는 원본 토큰)
    /// </summary>
    public HashSet<string> ValidTokens { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 커스텀 토큰 검증 델리게이트 (JWT 검증 등 동적 검증 시 활용)
    /// </summary>
    public Func<string, bool>? CustomTokenValidator { get; init; }

    /// <summary>
    /// 로컬 IPC(NamedPipe 등) 접근 허용 Windows User SID 또는 그룹 목록
    /// </summary>
    public HashSet<string> AllowedUserSids { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
