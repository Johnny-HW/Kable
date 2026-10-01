namespace Kable.Engine;

using System;

/// <summary>Optional default timeout contract; existing IDeviceSession implementations need not implement it.</summary>
public interface IRequestTimeoutProvider
{
    TimeSpan DefaultRequestTimeout { get; }
}
