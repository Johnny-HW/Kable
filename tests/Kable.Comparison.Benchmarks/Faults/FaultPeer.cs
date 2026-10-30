using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace Kable.Comparison.Faults;

// Scripted TCP peer: cancellation is triggered only after the request was observed.
public sealed class FaultPeer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _received = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _serve;
    private readonly string _script;
    public ConcurrentQueue<string> Commands { get; } = new();
    public Task Received => _received.Task;
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public FaultPeer(string script)
    {
        _script = script;
        _listener.Start();
        _serve = ServeAsync();
    }
    public void Release() => _release.TrySetResult();
    private async Task ServeAsync()
    {
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
            client.NoDelay = true;
            using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 4096, true);
            while (await reader.ReadLineAsync(_stop.Token) is { } command)
            {
                Commands.Enqueue(command);
                _received.TrySetResult();
                if (_script == "stall") await Task.Delay(Timeout.Infinite, _stop.Token);
                if (_script == "close")
                {
                    await _release.Task.WaitAsync(_stop.Token);
                    return; // Graceful FIN, not a simulated cable removal.
                }
                if (_script == "late" && command == "A") await _release.Task.WaitAsync(_stop.Token);
                await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(command + "\n"), _stop.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (IOException) { PeerDisconnected = true; }
        catch (SocketException) { PeerDisconnected = true; }
    }
    public bool PeerDisconnected { get; private set; }
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try { await _serve; }
        finally { _listener.Stop(); _stop.Dispose(); }
    }
}
