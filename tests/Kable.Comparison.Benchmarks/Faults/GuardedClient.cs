using Kable.Comparison.Adapters;
namespace Kable.Comparison.Faults;

// Common application policy, deliberately applied to EVERY library including Kable.
// It is not a claimed native capability of any third-party library.
public sealed class GuardedClient(IComparisonClient inner) : IComparisonClient
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _invalid;
    public Task ConnectAsync(int port, CancellationToken ct) => inner.ConnectAsync(port, ct);
    public async Task<string> QueryAsync(string request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await _gate.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            if (Volatile.Read(ref _invalid) != 0) throw new IOException("Session invalidated; create a fresh client.");
            try { return await inner.QueryAsync(request, ct); }
            catch
            {
                Interlocked.Exchange(ref _invalid, 1);
                throw;
            }
        }
        finally { _gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _invalid, 1);
        await inner.DisposeAsync();
        _gate.Dispose();
    }
}
