namespace Kable.Core.Security;

using System;
using Kable.Exceptions;
using Kable.Localization;

/// <summary>
/// 에이전트/장비가 이미 다른 작업을 실행 중이어서 신규 요청을 처리할 수 없을 때 발생하는 예외입니다.
/// </summary>
public class DeviceBusyException : KableException
{
    public string ResourceKey { get; }

    public DeviceBusyException(string resourceKey, string message = "Device or agent is currently busy processing another request.")
        : base(KableErrorCode.HardwareFault, message)
    {
        ResourceKey = resourceKey;
    }
}

/// <summary>
/// 악성 파일 실행 시도, 허용되지 않은 경로/바이너리 접근 또는 토큰 인증 실패 시 발생하는 보안 예외입니다.
/// </summary>
public class SecurityValidationException : KableException
{
    public string TargetResource { get; }

    public SecurityValidationException(string targetResource, string message)
        : base(KableErrorCode.ProtocolViolation, message)
    {
        TargetResource = targetResource;
    }

    public SecurityValidationException(string targetResource, string message, Exception innerException)
        : base(KableErrorCode.ProtocolViolation, message, innerException)
    {
        TargetResource = targetResource;
    }
}
