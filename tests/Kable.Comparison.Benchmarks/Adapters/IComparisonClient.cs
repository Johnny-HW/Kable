namespace Kable.Comparison.Adapters;

public interface IComparisonClient : IAsyncDisposable
{
    Task ConnectAsync(int port, CancellationToken ct);
    Task<string> QueryAsync(string request, CancellationToken ct);
}

public static class Clients
{
    public static readonly string[] Names = ["DotNet", "Kable", "TouchSocket", "SuperSocket", "DotNetty"];
    public static IComparisonClient Create(string name) => name switch
    {
        "DotNet" => new DotNetClient(),
        "Kable" => new KableClient(),
        "TouchSocket" => new TouchSocketClient(),
        "SuperSocket" => new SuperSocketClient(),
        "DotNetty" => new DotNettyClient(),
        _ => throw new ArgumentException("Unknown client", nameof(name))
    };
}
