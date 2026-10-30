namespace Kable.Tests.Cases.Simple.Reconnecting;

using Kable.Exceptions;
using Kable.Observability;
using Kable.Simple;
using Kable.Simple.Reconnecting;
using Xunit;

public sealed class RecoveryCommandRaceTests
{
    [Fact]
    public async Task ReleasingCapturedClientReportsDisconnectionForInFlightCommand()
    {
        var inner = new RacingClient();
        var wrapper = await ReconnectingKableClient.OpenAsync(_ => new(inner));
        var command = wrapper.QueryAsync("READ").AsTask();
        await inner.Started.Task;
        await wrapper.DisposeAsync();
        var error = await Assert.ThrowsAsync<DeviceDisconnectedException>(() => command);
        Assert.IsType<ObjectDisposedException>(error.InnerException);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => wrapper.QueryAsync("READ").AsTask());
    }

    private sealed class RacingClient : IKableSimpleClient
    {
        public bool IsConnected { get; private set; } = true;
        public CommHealthMetrics HealthMetrics => CommHealthMetrics.Empty;
        public event Action<string>? LineReceived { add { } remove { } }
        public event Action<Exception>? ErrorOccurred { add { } remove { } }
        public event Action<Exception?>? Disconnected { add { } remove { } }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<string> QueryAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            Started.TrySetResult();
            await _released.Task;
            throw new ObjectDisposedException(nameof(RacingClient));
        }
        public ValueTask SendLineAsync(string command, CancellationToken ct = default) => default;
        public ValueTask DisposeAsync() { IsConnected = false; _released.TrySetResult(); return default; }
        public void Dispose() { IsConnected = false; _released.TrySetResult(); }
    }
}
