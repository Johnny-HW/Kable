namespace Kable.Core.Security;

using System;
using System.Collections.Generic;

/// <summary>
/// 외부 프로세스 및 스크립트 실행(LAUNCH) 요청 시 악성 코드 실행을 방지하기 위한 허용 목록(Allowlist) 및 보안 검증 옵션 모델입니다.
/// </summary>
public record LaunchGuardOptions
{
    /// <summary>
    /// 화이트리스트 기반 보안 가드 강제 활성화 여부
    /// </summary>
    public bool EnableAllowlistOnly { get; init; } = true;

    /// <summary>
    /// 실행이 허용된 디렉터리 경로 목록.
    /// 모든 경로는 절대경로로 정규화(Path.GetFullPath)되어 디렉터리 트래버설(../) 공격을 원천 차단합니다.
    /// </summary>
    public HashSet<string> AllowedDirectories { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 실행이 허용된 바이너리/실행 파일 명칭 목록 (예: "ffmpeg.exe", "Kable.Worker.exe")
    /// 비어 있을 경우 AllowedDirectories 내의 모든 허용된 실행 파일이 대상이 됩니다.
    /// </summary>
    public HashSet<string> AllowedExecutableNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 실행 파일 무결성 검증을 위한 SHA-256 해시 매핑 (Key: 파일명 또는 정규화 경로, Value: 소문자 16진수 SHA-256 해시)
    /// 설정되어 있을 경우 실행 전 파일 해시를 계산하여 일치하지 않으면 실행을 거부합니다.
    /// </summary>
    public Dictionary<string, string> AllowedSha256Hashes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 실행이 금지된 위험 스크립트 확장자 목록 (기본: .bat, .cmd, .vbs, .ps1, .sh, .bash, .js, .vbe, .wsf)
    /// </summary>
    public HashSet<string> DisallowedExtensions { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat",
        ".cmd",
        ".vbs",
        ".ps1",
        ".sh",
        ".bash",
        ".js",
        ".vbe",
        ".wsf"
    };

    /// <summary>
    /// 명령줄 인자(Arguments)에 쉘 인젝션 메타문자(&, |, ;, `, $, >, <) 포함 여부를 엄격히 차단할지 여부
    /// </summary>
    public bool RestrictShellCharacters { get; init; } = true;
}
