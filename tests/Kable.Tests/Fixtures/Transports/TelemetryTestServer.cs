namespace Kable.Tests.Fixtures.Transports;

using System.Net;
using System.Net.Sockets;
using System.Text;

internal sealed class TelemetryTestServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource<bool> _sendAllowed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _received = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly int _packetCount;
    private string _phase = "accepting";
    private int _sent;

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public Task Completion { get; }
    public TaskCompletionSource<bool> Sent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TelemetryTestServer(int packetCount)
    {
        _packetCount = packetCount;
        _listener.Start();
        Completion = RunAsync();
    }

    public void StartSending() => _sendAllowed.TrySetResult(true);
    public void ConfirmReceived() => _received.TrySetResult(true);
    public string Snapshot() => $"Server phase={Volatile.Read(ref _phase)}, task={Completion.Status}, " +
        $"sent={Volatile.Read(ref _sent)}/{_packetCount}, clientAcknowledged={_received.Task.IsCompleted}";

    private async Task RunAsync()
    {
        var ct = _lifetime.Token;
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(ct);
            using var stream = client.GetStream();
            Volatile.Write(ref _phase, "waiting for send permission");
            await _sendAllowed.Task.WaitAsync(ct);
            var payload = Encoding.ASCII.GetBytes("$TELEMETRY,123.456,OK\n");
            Volatile.Write(ref _phase, "sending");
            for (int i = 0; i < _packetCount; i++)
            {
                await stream.WriteAsync(payload.AsMemory(), ct);
                Interlocked.Increment(ref _sent);
            }
            await stream.FlushAsync(ct);
            Volatile.Write(ref _phase, "waiting for client receive acknowledgement");
            Sent.TrySetResult(true);
            await _received.Task.WaitAsync(ct);
            Volatile.Write(ref _phase, "receive acknowledged");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Fixture shutdown cancels accept, writes and pending completion gates.
            Volatile.Write(ref _phase, "canceled");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        try { await Completion.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally
        {
            _listener.Stop();
            _lifetime.Dispose();
        }
    }
}
