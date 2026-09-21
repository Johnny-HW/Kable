namespace Kable.Exceptions;

using System;

using Kable.Localization;

public class KableException : Exception
{
    public KableErrorCode ErrorCode { get; }

    public KableException(KableErrorCode errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public KableException(KableErrorCode errorCode, string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// 현재 로컬라이저 설정에 맞춘 다국어 메시지 조회
    /// </summary>
    public virtual string GetLocalizedMessage() => KableLocalizer.Instance.GetErrorMessage(ErrorCode);
}

public class DeviceDisconnectedException : KableException
{
    public DeviceDisconnectedException(string message) 
        : base(KableErrorCode.DeviceDisconnected, message) { }

    public DeviceDisconnectedException(string message, Exception innerException) 
        : base(KableErrorCode.DeviceDisconnected, message, innerException) { }
}

public class DeviceTimeoutException : TimeoutException
{
    public KableErrorCode ErrorCode => KableErrorCode.DeviceTimeout;
    public string Command { get; }
    public TimeSpan Timeout { get; }

    public DeviceTimeoutException(string command, TimeSpan timeout)
        : base($"Device command '{command}' timed out after {timeout.TotalSeconds:F1}s.")
    {
        Command = command;
        Timeout = timeout;
    }

    public string GetLocalizedMessage() => 
        KableLocalizer.Instance.GetErrorMessage(ErrorCode, Command, (int)Timeout.TotalMilliseconds);
}

public class ProtocolViolationException : KableException
{
    public ProtocolViolationException(string message) 
        : base(KableErrorCode.ProtocolViolation, message) { }

    public ProtocolViolationException(string message, Exception innerException) 
        : base(KableErrorCode.ProtocolViolation, message, innerException) { }

    public override string GetLocalizedMessage() => 
        KableLocalizer.Instance.GetErrorMessage(ErrorCode, Message);
}

