using System.Net.Sockets;
using System.Text;
namespace Kable.Comparison.Adapters;

public sealed class DotNetClient : IComparisonClient
{
    private readonly TcpClient _client = new() { NoDelay = true };
    private StreamReader? _reader;
    public async Task ConnectAsync(int port, CancellationToken ct)
    {
        await _client.ConnectAsync("127.0.0.1", port, ct);
        _reader = new StreamReader(_client.GetStream(), Encoding.ASCII, false, 4096, true);
    }
    public async Task<string> QueryAsync(string request, CancellationToken ct)
    {
        await _client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(request + "\n"), ct);
        return await _reader!.ReadLineAsync(ct) ?? throw new EndOfStreamException();
    }
    public ValueTask DisposeAsync()
    {
        _reader?.Dispose();
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
