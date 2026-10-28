namespace Kable.Tests.Cases.Transports;

using System.IO.Ports;
using Kable.Transports;
using Xunit;

public sealed class SerialLifecycleTests
{
    [Fact]
    public async Task AbortThenDispose_ClosesOwnedStreamExactlyOnce()
    {
        using var port = new SerialPort("COM1");
        var stream = new TrackingStream();
        var context = new SerialPortConnectionContext(port, stream);
        var closed = context.ConnectionClosed;
        context.Abort("simulated removal");
        await Task.WhenAll(context.DisposeAsync().AsTask(), context.DisposeAsync().AsTask());
        Assert.True(closed.IsCancellationRequested);
        Assert.False(stream.CanRead);
        Assert.Equal(1, stream.CloseCount);
    }

    [Fact]
    public async Task PreCanceledConnect_DoesNotTryOpeningPort()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SerialPortConnectionFactory("COM_INVALID").ConnectAsync(cancel.Token).AsTask());
    }

    [Fact]
    public async Task CanceledOpenReturnsPromptlyAndDisposesLatePort()
    {
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = new CancellationTokenSource();
        var factory = new SerialPortConnectionFactory(async ct =>
        {
            opened.SetResult();
            await release.Task;
            return new SerialPortConnectionContext(new SerialPort("COM1"), new TrackingStream(() => cleaned.TrySetResult()));
        });
        var connect = factory.ConnectAsync(cancel.Token).AsTask();
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connect.WaitAsync(TimeSpan.FromSeconds(2)));
        release.SetResult();
        await cleaned.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class TrackingStream(Action? onClose = null) : MemoryStream
    {
        public int CloseCount;
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Increment(ref CloseCount) == 1) onClose?.Invoke();
            base.Dispose(disposing);
        }
    }
}
