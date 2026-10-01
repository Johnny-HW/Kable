namespace Kable.Tests.Cases.Simple.Reconnecting;

using Kable.Exceptions;
using Kable.Simple;
using Kable.Simple.Reconnecting;
using Xunit;

public class ReconnectingClientTests
{
    [Fact]
    public async Task Disconnect_CreatesFreshClient_AndPreservesSubscriptions()
    {
        var first = new FakeClient();
        var second = new FakeClient();
        int opens = 0;
        await using var client = await ReconnectingKableClient.OpenAsync(_ =>
            new ValueTask<IKableSimpleClient>(Interlocked.Increment(ref opens) == 1 ? first : second), Options());
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var line = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Reconnected += () => restored.TrySetResult();
        client.LineReceived += text => line.TrySetResult(text);
        first.Drop();
        await restored.Task.WaitAsync(TimeSpan.FromSeconds(3));
        second.Emit("$NEW");
        Assert.Equal("$NEW", await line.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.True(first.Disposed);
        Assert.Equal(2, opens);
        Assert.Equal("OK", await client.QueryAsync("READ"));
    }

    [Fact]
    public async Task FailedCommand_IsNotReplayedAfterRecovery()
    {
        var first = new FakeClient { FailQuery = true };
        var second = new FakeClient();
        int opens = 0;
        await using var client = await ReconnectingKableClient.OpenAsync(_ =>
            new ValueTask<IKableSimpleClient>(Interlocked.Increment(ref opens) == 1 ? first : second), Options());
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Reconnected += () => restored.TrySetResult();
        await Assert.ThrowsAsync<DeviceDisconnectedException>(() => client.QueryAsync("MOVE").AsTask());
        await restored.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, first.Queries);
        Assert.Equal(0, second.Queries);
    }

    [Fact]
    public async Task RetryLimit_StopsRecovery_AndSurfacesFailure()
    {
        var first = new FakeClient();
        int opens = 0;
        await using var client = await ReconnectingKableClient.OpenAsync(_ =>
        {
            if (Interlocked.Increment(ref opens) == 1) return new ValueTask<IKableSimpleClient>(first);
            throw new IOException("offline");
        }, Options());
        first.Drop();
        await client.RecoveryTask.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(3, opens);
        Assert.IsType<IOException>(client.LastError);
        Assert.False(client.IsConnected);
        await Assert.ThrowsAsync<DeviceDisconnectedException>(() => client.QueryAsync("READ").AsTask());
    }

    [Fact]
    public async Task Dispose_CancelsConnectingFactory_AndPreventsFurtherAttempts()
    {
        var first = new FakeClient();
        var connecting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int opens = 0;
        var client = await ReconnectingKableClient.OpenAsync(async ct =>
        {
            if (Interlocked.Increment(ref opens) == 1) return first;
            connecting.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return new FakeClient();
        }, Options());
        first.Drop();
        await connecting.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(first.Disposed);
        Assert.Equal(2, opens);
        Assert.True(client.RecoveryTask.IsCompleted);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.QueryAsync("READ").AsTask());
    }

    [Fact]
    public async Task InitialConnectionFailure_IsReturnedToCaller()
    {
        await Assert.ThrowsAsync<IOException>(() => ReconnectingKableClient.OpenAsync(
            _ => throw new IOException("initial"), Options()).AsTask());
    }

    private static ReconnectOptions Options() => new()
    { RetryDelay = TimeSpan.FromMilliseconds(10), MaxAttempts = 2, ConnectTimeout = TimeSpan.FromSeconds(1) };

    private sealed class FakeClient : IKableSimpleClient
    {
        public bool IsConnected { get; private set; } = true;
        public bool Disposed { get; private set; }
        public bool FailQuery { get; init; }
        public int Queries { get; private set; }
        public event Action<string>? LineReceived;
        public event Action<Exception>? ErrorOccurred;
        public event Action<Exception?>? Disconnected;
        public void Emit(string text) => LineReceived?.Invoke(text);
        public void Drop()
        {
            IsConnected = false;
            Disconnected?.Invoke(new DeviceDisconnectedException("dropped"));
        }
        public ValueTask SendLineAsync(string command, CancellationToken ct = default) => default;
        public ValueTask<string> QueryAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            Queries++;
            if (FailQuery)
            {
                Drop();
                var error = new DeviceDisconnectedException("not executed again");
                ErrorOccurred?.Invoke(error);
                throw error;
            }
            return new("OK");
        }
        public void Dispose() { Disposed = true; IsConnected = false; }
        public ValueTask DisposeAsync() { Dispose(); return default; }
    }
}
