namespace Kable.Tests.Cases.Engine.Server;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class KableServerSafetyTests
{
    [Fact]
    public async Task CanceledStart_DoesNotStartServer()
    {
        await using var server = Create(new TestMemoryConnectionListener());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Func<Task> start = () => server.StartAsync(cts.Token);
        await start.Should().ThrowAsync<OperationCanceledException>();
        server.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StoppedServer_CannotRestart()
    {
        await using var server = Create(new TestMemoryConnectionListener());
        await server.StartAsync();
        await server.StopAsync();
        Func<Task> start = () => server.StartAsync();
        await start.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DisposedServer_CannotStart()
    {
        var server = Create(new TestMemoryConnectionListener());
        await server.DisposeAsync();
        Func<Task> start = () => server.StartAsync();
        await start.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ConnectionLimit_RejectsExcessConnection()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = Create(listener, new KableServerOptions { MaxConnections = 1 });
        var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += (_, _) => connected.TrySetResult(true);
        await server.StartAsync();
        var (first, client1) = TestDuplexPipePair.Create();
        var (excess, client2) = TestDuplexPipePair.Create();
        await using var owned1 = client1;
        await using var owned2 = client2;
        listener.EnqueueConnection(first);
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var rejected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = excess.ConnectionClosed.Register(() => rejected.TrySetResult(true));
        listener.EnqueueConnection(excess);
        await rejected.Task.WaitAsync(TimeSpan.FromSeconds(3));
        server.ActiveSessionCount.Should().Be(1);
        server.TryGetSession(excess.ConnectionId, out _).Should().BeFalse();
    }

    [Fact]
    public async Task CodecFactoryFailure_ReleasesConnectionAndAcceptsNextClient()
    {
        var listener = new TestMemoryConnectionListener();
        int attempts = 0;
        await using var server = new KableServer<string>(listener, () =>
            Interlocked.Increment(ref attempts) == 1 ? throw new InvalidOperationException("codec fault") : new AsciiLineCodec());
        var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += (_, _) => connected.TrySetResult(true);
        await server.StartAsync();
        var (failed, client1) = TestDuplexPipePair.Create();
        var (next, client2) = TestDuplexPipePair.Create();
        await using var owned1 = client1;
        await using var owned2 = client2;
        listener.EnqueueConnection(failed);
        listener.EnqueueConnection(next);
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(3));
        failed.ConnectionClosed.IsCancellationRequested.Should().BeTrue();
        server.ActiveSessionCount.Should().Be(1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void InvalidCapacity_IsRejected(int connections, int queue)
    {
        Action create = () => Create(new TestMemoryConnectionListener(), new KableServerOptions
        {
            MaxConnections = connections, SessionQueueCapacity = queue
        });
        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task BroadcastBeforeStart_IsRejected()
    {
        await using var server = Create(new TestMemoryConnectionListener());
        Func<Task> broadcast = async () => await server.BroadcastAsync("hello");
        await broadcast.Should().ThrowAsync<InvalidOperationException>();
    }

    private static KableServer<string> Create(TestMemoryConnectionListener listener, KableServerOptions? options = null)
        => new(listener, () => new AsciiLineCodec(), options);
}
