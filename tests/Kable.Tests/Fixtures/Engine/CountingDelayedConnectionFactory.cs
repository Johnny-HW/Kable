namespace Kable.Tests.Fixtures.Engine;

using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Tests.Fixtures;

internal sealed class CountingDelayedConnectionFactory : IConnectionFactory
{
    private int _connectCount;
    private readonly TaskCompletionSource<bool> _startedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _gateTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int ConnectInvocationCount => Volatile.Read(ref _connectCount);
    public Task ConnectStarted => _startedTcs.Task;

    public void AllowConnectToComplete() => _gateTcs.TrySetResult(true);

    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _connectCount);
        _startedTcs.TrySetResult(true);
        await _gateTcs.Task;
        return new TestMemoryConnectionContext();
    }
}
