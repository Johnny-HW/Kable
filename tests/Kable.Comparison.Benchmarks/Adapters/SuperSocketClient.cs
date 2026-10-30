using System.Buffers;
using System.Net;
using System.Text;
using SuperSocket.Client;
using SuperSocket.ProtoBase;
namespace Kable.Comparison.Adapters;

public sealed class SuperSocketClient : IComparisonClient
{
    private readonly IEasyClient<string> _client = new EasyClient<string>(new LfFilter());
    private readonly LineInbox _inbox = new();
    private bool _connected;
    public async Task ConnectAsync(int port, CancellationToken ct)
    {
        _client.PackageHandler += (_, line) => { _inbox.Put(line); return ValueTask.CompletedTask; };
        _client.Closed += (_, _) => _inbox.Close();
        if (!await _client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), ct))
            throw new IOException("SuperSocket connection failed");
        _connected = true;
        _client.StartReceive();
    }
    public async Task<string> QueryAsync(string request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await _client.SendAsync(Encoding.ASCII.GetBytes(request + "\n")).AsTask().WaitAsync(ct);
        return await _inbox.ReadAsync(ct);
    }
    public async ValueTask DisposeAsync()
    {
        if (_connected) await _client.CloseAsync();
        _inbox.Close();
    }
    private sealed class LfFilter() : TerminatorPipelineFilter<string>(new byte[] { 10 })
    {
        protected override string DecodePackage(ref ReadOnlySequence<byte> buffer) =>
            Encoding.ASCII.GetString(buffer);
    }
}

