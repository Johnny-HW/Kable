namespace Kable.Transport.Ipc.Tests;

using Kable.Transport.Ipc;
using Xunit;

public sealed class LifetimeRegressionTests
{
    [Fact]
    public async Task ConcurrentDisposeCallsShareCompletion()
    {
        var stream = new DelayedDisposeStream();
        var context = new IpcNamedPipeConnectionContext(stream, "test");
        var first = context.DisposeAsync().AsTask();
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = context.DisposeAsync().AsTask();
        Assert.False(second.IsCompleted);
        stream.Finish.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class DelayedDisposeStream : MemoryStream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask DisposeAsync()
        {
            Started.TrySetResult();
            await Finish.Task;
            await base.DisposeAsync();
        }
    }

    [Fact]
    public async Task AbortDoesNotSkipDisposeCleanup()
    {
        var context = new IpcNamedPipeConnectionContext(new MemoryStream(), "test");
        context.Abort("test");
        await context.DisposeAsync();
        await context.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => context.ConnectionClosed);
        context.Abort("after dispose");
    }

    [Fact]
    public async Task StopOnlyClosesPendingAcceptNotTransferredConnection()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        string name = "kable_lifetime_" + Guid.NewGuid().ToString("N");
        await using var listener = new IpcNamedPipeServerListener(name);
        var accepting = listener.AcceptAsync(deadline.Token).AsTask();
        await using var client = await new IpcNamedPipeClientFactory(name).ConnectAsync(deadline.Token);
        await using var accepted = await accepting;
        listener.Stop();
        var sending = client.Output.WriteAsync(new byte[] { 42 }, deadline.Token).AsTask();
        var read = await accepted.Input.ReadAsync(deadline.Token);
        Assert.Equal(1, read.Buffer.Length);
        accepted.Input.AdvanceTo(read.Buffer.End);
        await sending;
    }

    [Fact]
    public async Task CancelledAcceptDoesNotPreventSubsequentAccept()
    {
        string name = "kable_cancel_" + Guid.NewGuid().ToString("N");
        await using var listener = new IpcNamedPipeServerListener(name);
        using var cancelled = new CancellationTokenSource();
        var first = listener.AcceptAsync(cancelled.Token).AsTask();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var second = listener.AcceptAsync(deadline.Token).AsTask();
        await using var client = await new IpcNamedPipeClientFactory(name).ConnectAsync(deadline.Token);
        await using var server = await second;
    }
}
