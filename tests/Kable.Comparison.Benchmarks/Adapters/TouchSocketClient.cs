using System.Text;
using TouchSocket.Core;
using TouchSocket.Sockets;
namespace Kable.Comparison.Adapters;

public sealed class TouchSocketClient : IComparisonClient
{
    private readonly TouchSocket.Sockets.TcpClient _client = new();
    private readonly LineInbox _inbox = new();
    public async Task ConnectAsync(int port, CancellationToken ct)
    {
        _client.Received = (_, e) => { _inbox.Receive(e.Memory.Span); return Task.CompletedTask; };
        _client.Closed = (_, _) => { _inbox.Close(); return Task.CompletedTask; };
        await _client.SetupAsync(new TouchSocketConfig()
            .SetRemoteIPHost($"tcp://127.0.0.1:{port}").SetNoDelay(true));
        await _client.ConnectAsync().WaitAsync(ct);
    }
    public async Task<string> QueryAsync(string request, CancellationToken ct)
    {
        await _client.SendAsync(Encoding.ASCII.GetBytes(request + "\n")).WaitAsync(ct);
        return await _inbox.ReadAsync(ct);
    }
    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _inbox.Close();
        return ValueTask.CompletedTask;
    }
}
