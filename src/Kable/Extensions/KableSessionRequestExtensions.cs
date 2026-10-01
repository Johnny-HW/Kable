namespace Kable.Extensions;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Engine;

public static class KableSessionRequestExtensions
{
    /// <summary>Requests a response of the session message type. Explicit timeout overrides the configured default.</summary>
    public static ValueTask<TMessage> RequestAsync<TMessage>(this IDeviceSession<TMessage> session,
        TMessage request, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        var effective = timeout ?? (session as IRequestTimeoutProvider)?.DefaultRequestTimeout ?? TimeSpan.FromSeconds(3);
        if (effective <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        return session.RequestAsync<TMessage>(request, effective, ct);
    }
}
