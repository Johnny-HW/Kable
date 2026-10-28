namespace Kable.Exceptions;

using Kable.Localization;

/// <summary>The bounded telemetry delivery backlog is saturated; the session cannot preserve further data.</summary>
public sealed class TelemetryBufferOverflowException : KableException
{
    public TelemetryBufferOverflowException(string message) : base(KableErrorCode.BufferOverflow, message) { }
}
