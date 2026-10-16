namespace Kable.Tests.Fixtures.Engine;

using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Tests.Fixtures;

internal sealed class ControllableConnectionFactory : IConnectionFactory
{
    private readonly TaskCompletionSource<bool> _connectStartedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _connectGateTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task ConnectStarted => _connectStartedTcs.Task;
    public TestMemoryConnectionContext? LastCreatedContext { get; private set; }

    public void AllowConnectToComplete()
    {
        _connectGateTcs.TrySetResult(true);
    }

    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        _connectStartedTcs.TrySetResult(true);
        await _connectGateTcs.Task;
        var ctx = new TestMemoryConnectionContext();
        LastCreatedContext = ctx;
        return ctx;
    }
}
