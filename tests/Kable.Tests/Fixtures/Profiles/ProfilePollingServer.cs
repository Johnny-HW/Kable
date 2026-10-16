namespace Kable.Tests.Fixtures.Profiles;

using System.Net;
using System.Net.Sockets;
using System.Text;

internal sealed class ProfilePollingServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource<bool> _responsesAllowed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly int _firstResponseDelayMs;
    private string _phase = "accepting";
    private string? _lastCommand;
    private int _requests;
    private int _responses;

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public Task Completion { get; }
    public TaskCompletionSource<bool> FirstRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ProfilePollingServer(int firstResponseDelayMs)
    {
        _firstResponseDelayMs = firstResponseDelayMs;
        _listener.Start();
        Completion = RunAsync();
    }

    public void AllowResponses() => _responsesAllowed.TrySetResult(true);

    public string Snapshot() => $"Server phase={Volatile.Read(ref _phase)}, task={Completion.Status}, " +
        $"requests={Volatile.Read(ref _requests)}, responses={Volatile.Read(ref _responses)}, lastCommand={Volatile.Read(ref _lastCommand)}";

    private async Task RunAsync()
    {
        var ct = _lifetime.Token;
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(ct);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true) { NewLine = "\n" };
            while (true)
            {
                Volatile.Write(ref _phase, "reading command");
                var line = await reader.ReadLineAsync(ct);
                if (line == null)
                {
                    Volatile.Write(ref _phase, "peer closed");
                    return;
                }
                Volatile.Write(ref _lastCommand, line);
                int request = Interlocked.Increment(ref _requests);
                FirstRequest.TrySetResult(true);
                Volatile.Write(ref _phase, "waiting for response permission");
                await _responsesAllowed.Task.WaitAsync(ct);
                if (request == 1 && _firstResponseDelayMs > 0)
                {
                    Volatile.Write(ref _phase, "injected first response delay");
                    await Task.Delay(_firstResponseDelayMs, ct);
                }
                var reply = line switch
                {
                    "?STATUS" => "STATUS:READY",
                    "?PRESSURE" => "PRESSURE:101.3",
                    "CMD:INIT" => "OK:INIT",
                    _ => throw new InvalidDataException($"Unexpected command: {line}")
                };
                Volatile.Write(ref _phase, "writing response");
                await writer.WriteLineAsync(reply.AsMemory(), ct);
                await writer.FlushAsync(ct);
                Interlocked.Increment(ref _responses);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Fixture shutdown cancels accept, reads, gates and injected delays.
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
