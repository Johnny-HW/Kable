namespace Kable.Tests.Fixtures.Engine;

using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Tests.Fixtures;

internal sealed class FailingConnectionFactory : IConnectionFactory
{
    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        throw new System.Net.Sockets.SocketException(10061); // Connection Refused
    }
}
