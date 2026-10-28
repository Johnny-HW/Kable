namespace Kable.Tests.Cases.Engine.Server;

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class KableServerShutdownTests
{
    [Fact]
    public async Task Shutdown_DrainsAcceptedMessages()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new AsciiLineCodec());
        var connected = Connected(server);
        var (context, client) = TestDuplexPipePair.Create();
        await using var owned = client;
        await server.StartAsync();
        listener.EnqueueConnection(context);
        var session = await connected.WaitAsync(TimeSpan.FromSeconds(3));
        await session.SendAsync("ONE");
        await session.SendAsync("TWO");
        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        var read = await client.Input.ReadAsync();
        Encoding.ASCII.GetString(read.Buffer.ToArray()).Should().Be("ONE\nTWO\n");
        client.Input.AdvanceTo(read.Buffer.End);
        server.ActiveSessionCount.Should().Be(0);
    }

    [Fact]
    public async Task Shutdown_AbortsBlockedFlushAfterDrainDeadline()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new AsciiLineCodec(),
            new KableServerOptions { SessionQueueCapacity = 1 });
        var connected = Connected(server);
        var (context, client) = TestDuplexPipePair.Create(serverToClientOptions:
            new PipeOptions(pauseWriterThreshold: 1, resumeWriterThreshold: 1));
        await using var owned = client;
        await server.StartAsync();
        listener.EnqueueConnection(context);
        var session = await connected.WaitAsync(TimeSpan.FromSeconds(3));
        await session.SendAsync("BLOCKED");
        await session.SendAsync("QUEUED");
        await server.StopAsync(TimeSpan.FromMilliseconds(50)).WaitAsync(TimeSpan.FromSeconds(3));
        context.ConnectionClosed.IsCancellationRequested.Should().BeTrue();
        server.ActiveSessionCount.Should().Be(0);
    }

    [Fact]
    public async Task StopCallerCancellation_DoesNotCancelSharedCleanup()
    {
        var listener = new TestMemoryConnectionListener();
        await using var server = new KableServer<string>(listener, () => new AsciiLineCodec());
        var connected = Connected(server);
        var (context, client) = TestDuplexPipePair.Create(serverToClientOptions:
            new PipeOptions(pauseWriterThreshold: 1, resumeWriterThreshold: 1));
        await using var owned = client;
        await server.StartAsync();
        listener.EnqueueConnection(context);
        var session = await connected.WaitAsync(TimeSpan.FromSeconds(3));
        await session.SendAsync("BLOCKED");
        using var cts = new CancellationTokenSource();
        var stop = server.StopAsync(TimeSpan.FromMilliseconds(100), cts.Token);
        cts.Cancel();
        Func<Task> canceled = () => stop;
        await canceled.Should().ThrowAsync<OperationCanceledException>();
        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        server.ActiveSessionCount.Should().Be(0);
    }

    [Fact]
    public async Task SessionQueue_UsesExactConfiguredCapacity()
    {
        var (context, client) = TestDuplexPipePair.Create();
        await using var owned = client;
        await using var session = new KableServerSession<string>(context, new AsciiLineCodec(), 1);
        await session.SendAsync("FIRST");
        using var cts = new CancellationTokenSource();
        var pending = session.SendAsync("SECOND", cts.Token).AsTask();
        pending.IsCompleted.Should().BeFalse();
        cts.Cancel();
        Func<Task> send = () => pending;
        await send.Should().ThrowAsync<OperationCanceledException>();
    }

    private static Task<IServerSession<string>> Connected(KableServer<string> server)
    {
        var connected = new TaskCompletionSource<IServerSession<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += (_, args) => connected.TrySetResult(args.Session);
        return connected.Task;
    }
}
