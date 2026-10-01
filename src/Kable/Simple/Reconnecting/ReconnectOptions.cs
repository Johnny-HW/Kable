namespace Kable.Simple.Reconnecting;

using System;

/// <summary>Finite retries per outage. Factories must honor cancellation and return a new, open client.</summary>
public sealed record ReconnectOptions
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);

    internal void Validate()
    {
        if (MaxAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(MaxAttempts));
        if (RetryDelay <= TimeSpan.Zero || RetryDelay.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(RetryDelay));
        if (ConnectTimeout <= TimeSpan.Zero || ConnectTimeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(ConnectTimeout));
    }
}
