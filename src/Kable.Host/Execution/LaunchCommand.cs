namespace Kable.Host.Execution;

using System.Collections.Generic;

/// <summary>
/// 외부 프로세스 및 실행 파일 구동 요청을 나타내는 불변 커맨드 모델입니다.
/// </summary>
public sealed record LaunchCommand
{
    /// <summary>
    /// 실행할 바이너리/프로그램 경로
    /// </summary>
    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>
    /// 명령줄 인자 (옵션)
    /// </summary>
    public string? Arguments { get; init; }

    /// <summary>
    /// 작업 디렉터리 (옵션)
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// 환경 변수 오버라이드 딕셔너리
    /// </summary>
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }
}
