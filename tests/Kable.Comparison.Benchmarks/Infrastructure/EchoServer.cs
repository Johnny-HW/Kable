using System.Net;
using System.Net.Sockets;
using System.Text;
namespace Kable.Comparison.Infrastructure;

public sealed class EchoServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<Task> _connections = [];
    private readonly Task _accept;
    private readonly bool _fragment;
    private readonly bool _closeOnRequest;
    private readonly bool _stallOnRequest;
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public EchoServer(bool fragment = false, bool closeOnRequest = false, bool stallOnRequest = false)
    {
        _fragment = fragment;
        _closeOnRequest = closeOnRequest;
        _stallOnRequest = stallOnRequest;
        _listener.Start();
        _accept = AcceptAsync();
    }
    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                client.NoDelay = true;
                _connections.Add(ServeAsync(client));
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }
    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        using (var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 4096, true))
        {
            try
            {
                while (await reader.ReadLineAsync(_stop.Token) is { } line)
                {
                    if (_closeOnRequest) return;
                    if (_stallOnRequest) await Task.Delay(Timeout.Infinite, _stop.Token);
                    var bytes = Encoding.ASCII.GetBytes(line + "\n");
                    if (_fragment)
                    {
                        await client.GetStream().WriteAsync(bytes.AsMemory(0, 1), _stop.Token);
                        await Task.Delay(2, _stop.Token);
                        // The remaining payload and delimiter arrive together.
                        await client.GetStream().WriteAsync(bytes.AsMemory(1), _stop.Token);
                    }
                    else await client.GetStream().WriteAsync(bytes, _stop.Token);
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (IOException) when (_stop.IsCancellationRequested) { }
        }
    }
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try
        {
            await _accept;
            await Task.WhenAll(_connections);
        }
        finally
        {
            _listener.Stop();
            _stop.Dispose();
        }
    }
}
