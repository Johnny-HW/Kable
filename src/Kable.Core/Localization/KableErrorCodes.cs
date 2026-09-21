namespace Kable.Localization;

/// <summary>
/// Kable 표준 에러 코드 정의
/// </summary>
public enum KableErrorCode
{
    None = 0,
    DeviceDisconnected = 1001,
    DeviceTimeout = 1002,
    ProtocolViolation = 1003,
    InvalidCast = 1004,
    ConnectionFailed = 1005,
    OperationCanceled = 1006,
    BufferOverflow = 1007,
    HeartbeatLost = 1008,
    HardwareFault = 1009
}
