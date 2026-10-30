using Kable.Simple;
namespace Kable.Comparison.Adapters;

public sealed class KableClient : IComparisonClient
{
    private IKableSimpleClient? _client;
    public async Task ConnectAsync(int port, CancellationToken ct) =>
        _client = await KableSimple.OpenTcpAsync("127.0.0.1", port, ct: ct);
    public async Task<string> QueryAsync(string request, CancellationToken ct) =>
        await _client!.QueryAsync(request, TimeSpan.FromSeconds(10), ct);
    public async ValueTask DisposeAsync()
    {
        if (_client is not null) await _client.DisposeAsync();
    }
}
