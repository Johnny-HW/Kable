namespace Kable.Tests.Cases.Engine.Lifecycle;

using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Observability;
using Kable.Transports;
using Xunit;

public sealed class SessionEngineHardeningTests
{
    [Fact]
    public async Task RequestAsync_WithCustomTimeProvider_UsesTimeProviderEpochForLatency()
    {
        var timeProvider = new OffsetTimeProvider(offsetSeconds: 10_000);
        var (clientCtx, serverCtx) = InMemoryConnectionContext.CreatePair();
        var observer = new RecordingObserver();

        await using var session = new KableSession<string>(
            new StaticFactory(clientCtx),
            new AsciiLineCodec(),
            observer: observer,
            heartbeatOptions: new HeartbeatOptions<string>(
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5),
                () => "HB",
                timeProvider: timeProvider));

        await session.StartAsync();

        var responder = Task.Run(async () =>
        {
            var read = await serverCtx.Input.ReadAsync();
            serverCtx.Input.AdvanceTo(read.Buffer.End);
            timeProvider.Advance(TimeSpan.FromMilliseconds(42));
            await serverCtx.Output.WriteAsync(Encoding.ASCII.GetBytes("PONG\n"));
            await serverCtx.Output.FlushAsync();
        });

        var reply = await session.RequestAsync<string>("PING", TimeSpan.FromSeconds(5));
        await responder;

        reply.Should().Be("PONG");
        observer.Records.Should().Contain(r => r.Tag == "RESP_RX" && r.Latency >= TimeSpan.FromMilliseconds(42) && r.Latency < TimeSpan.FromSeconds(5));
        await serverCtx.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_WhenConnectReturnsAfterDisposeTimeout_DisposesOrphanContextWithoutObjectDisposedException()
    {
        var connectEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowConnectReturn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (clientCtx, serverCtx) = InMemoryConnectionContext.CreatePair();
        var trackedCtx = new TrackingConnectionContext(clientCtx);

        var session = new KableSession<string>(
            new DelayedUncancellableFactory(connectEntered, allowConnectReturn, trackedCtx),
            new AsciiLineCodec());

        var startTask = session.StartAsync().AsTask();
        await connectEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // DisposeAsync waits up to 2s for StartAsync and then disposes _lifecycleLock.
        await session.DisposeAsync();

        // Now let ConnectAsync return after _lifecycleLock has been disposed.
        allowConnectReturn.TrySetResult(true);

        var ex = await Assert.ThrowsAsync<OperationCanceledException>(async () => await startTask);
        ex.Message.Should().Contain("stopped or disposed");
        trackedCtx.DisposeCount.Should().BeGreaterThanOrEqualTo(1);
        await serverCtx.DisposeAsync();
    }

    private sealed class OffsetTimeProvider(long offsetSeconds) : TimeProvider
    {
        private long _ticks = offsetSeconds * TimeSpan.TicksPerSecond;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Volatile.Read(ref _ticks);
        public void Advance(TimeSpan delta) => Interlocked.Add(ref _ticks, delta.Ticks);
    }

    private sealed class StaticFactory(IConnectionContext ctx) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(ctx);
    }

    private sealed class DelayedUncancellableFactory(
        TaskCompletionSource<bool> entered,
        TaskCompletionSource<bool> release,
        IConnectionContext ctx) : IConnectionFactory
    {
        public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
        {
            entered.TrySetResult(true);
            await release.Task.ConfigureAwait(false);
            return ctx;
        }
    }

    private sealed class TrackingConnectionContext(IConnectionContext inner) : IConnectionContext
    {
        private int _disposeCount;
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public string ConnectionId => inner.ConnectionId;
        public string EndpointDescription => inner.EndpointDescription;
        public PipeReader Input => inner.Input;
        public PipeWriter Output => inner.Output;
        public CancellationToken ConnectionClosed => inner.ConnectionClosed;
        public void Abort(string reason) => inner.Abort(reason);
        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            return inner.DisposeAsync();
        }
    }

    private sealed class RecordingObserver : ICommObserver
    {
        public ConcurrentQueue<PacketTraceRecord> Records { get; } = new();
        public ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
        public ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
        public ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();
        public void OnPacketTrace(in PacketTraceRecord trace) => Records.Enqueue(trace);
    }
}
