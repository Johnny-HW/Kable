namespace Kable.Tests.Cases.Engine.Lifecycle;

using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class ConcurrentStartCancellationTests
{
    [Fact]
    public async Task CancelingOneWaiter_PreservesStarterAndOtherWaiters()
    {
        var factory = new GatedFactory();
        await using var session = new KableSession<string>(factory, new AsciiLineCodec());
        var starter = session.StartAsync().AsTask();
        await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var canceledWaiter = session.StartAsync(cancellation.Token).AsTask();
        var otherWaiter = session.StartAsync().AsTask();

        try
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => canceledWaiter.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.False(starter.IsCompleted);
            Assert.False(otherWaiter.IsCompleted);
            Assert.False(factory.ConnectionToken.IsCancellationRequested);
            Assert.Equal(SessionLifecycleState.Starting, session.State);
            Assert.Equal(1, factory.Calls);
        }
        finally
        {
            factory.Gate.TrySetResult(true);
            await Task.WhenAll(starter, otherWaiter).WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.True(session.IsConnected);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public async Task AlreadyCanceledWaiter_DoesNotAffectSharedConnection()
    {
        var factory = new GatedFactory();
        await using var session = new KableSession<string>(factory, new AsciiLineCodec());
        var starter = session.StartAsync().AsTask();
        await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await session.StartAsync(cancellation.Token));
            Assert.False(factory.ConnectionToken.IsCancellationRequested);
            Assert.Equal(1, factory.Calls);
        }
        finally
        {
            factory.Gate.TrySetResult(true);
            await starter.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task ConnectionFailure_IsSharedWithUncanceledWaiters()
    {
        var expected = new InvalidOperationException("Injected connect failure");
        var factory = new GatedFactory { Failure = expected };
        var session = new KableSession<string>(factory, new AsciiLineCodec());
        try
        {
            var starter = session.StartAsync().AsTask();
            await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var waiter = session.StartAsync().AsTask();
            factory.Gate.TrySetResult(true);
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => starter));
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => waiter));
            Assert.Equal(1, factory.Calls);
            Assert.False(session.IsConnected);
        }
        finally
        {
            factory.Gate.TrySetResult(true);
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await session.DisposeAsync()));
        }
    }

    private sealed class GatedFactory : IConnectionFactory
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken ConnectionToken { get; private set; }
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }

        public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
        {
            Calls++;
            ConnectionToken = ct;
            Entered.TrySetResult(true);
            await Gate.Task.WaitAsync(ct);
            if (Failure != null) throw Failure;
            return new TestMemoryConnectionContext();
        }
    }
}
