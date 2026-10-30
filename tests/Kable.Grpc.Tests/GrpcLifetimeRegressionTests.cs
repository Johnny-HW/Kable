namespace Kable.Grpc.Tests;

using global::Grpc.Core;
using Kable.Grpc.Protos;
using Xunit;

public sealed class GrpcLifetimeRegressionTests
{
    [Fact]
    public async Task ConcurrentDisposeCallsWaitForTheSamePumpShutdown()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new WaitingReader { Release = release };
        var context = new GrpcConnectionContext(reader, new Sink(), "test");
        context.Abort("test");
        var first = context.DisposeAsync().AsTask();
        var second = context.DisposeAsync().AsTask();
        Assert.False(second.IsCompleted);
        release.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(reader.Exited);
    }

    [Fact]
    public async Task AbortThenDisposeAwaitsPumpsAndDisposesTokenSource()
    {
        var reader = new WaitingReader();
        var context = new GrpcConnectionContext(reader, new Sink(), "test");
        context.Abort("test");
        await context.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(reader.Exited);
        Assert.Throws<ObjectDisposedException>(() => context.ConnectionClosed);
        context.Abort("again");
        await context.DisposeAsync();
    }
    private sealed class WaitingReader : IAsyncStreamReader<KablePacket>
    {
        public KablePacket Current => new();
        public bool Exited;
        public TaskCompletionSource? Release { get; init; }
        public async Task<bool> MoveNext(CancellationToken ct)
        {
            try { await Task.Delay(Timeout.Infinite, ct); return false; }
            finally
            {
                if (Release != null) await Release.Task;
                Exited = true;
            }
        }
    }
    private sealed class Sink : IServerStreamWriter<KablePacket>
    {
        public WriteOptions? WriteOptions { get; set; }
        public Task WriteAsync(KablePacket message) => Task.CompletedTask;
    }
}
