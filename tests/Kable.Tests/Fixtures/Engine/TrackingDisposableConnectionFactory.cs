namespace Kable.Tests.Fixtures.Engine;

using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Tests.Fixtures;

internal sealed class TrackingDisposableConnectionFactory : IConnectionFactory
{
    public TrackingDisposableConnectionContext? LastCreatedContext { get; private set; }

    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        var ctx = new TrackingDisposableConnectionContext();
        LastCreatedContext = ctx;
        return new ValueTask<IConnectionContext>(ctx);
    }
}
