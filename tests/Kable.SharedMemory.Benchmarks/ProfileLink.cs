namespace Kable.SharedMemory.Benchmarks;

using System.Text;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;

internal sealed class ProfileLink : IAsyncDisposable
{
    internal IConnectionContext Server = null!;
    internal KableSession<string> Session = null!;
    internal byte[] Telemetry = null!;
    internal string TelemetryText = null!;
    private IConnectionListener? _listener;
    private Task _echo = Task.CompletedTask;
    private string _request = null!;
    private string _replyText = null!;

    internal static async Task<ProfileLink> CreateAsync(string mode, int characters, bool stream, CancellationToken ct)
    {
        string channel = "profile_" + Guid.NewGuid().ToString("N");
        var link = new ProfileLink();
        IConnectionContext client;
        if (mode == "raw")
        {
            link.Server = SharedMemoryTransport.CreateServerEndpoint(channel);
            client = SharedMemoryTransport.ConnectClientEndpoint(channel);
        }
        else
        {
            // Reflection keeps the identical profiler source buildable against the pre-optimization transport.
            var assembly = typeof(SharedMemoryTransport).Assembly;
            Type listenerType = assembly.GetType("Kable.SharedMemory.Connections.SharedMemoryServerListener", throwOnError: true)!;
            Type factoryType = assembly.GetType("Kable.SharedMemory.Connections.SharedMemoryClientFactory", throwOnError: true)!;
            link._listener = (IConnectionListener)Activator.CreateInstance(listenerType, channel, 65536, 5000)!;
            Task<IConnectionContext> accepted = link._listener.AcceptAsync(ct).AsTask();
            client = await ((IConnectionFactory)Activator.CreateInstance(factoryType, channel, 5000)!).ConnectAsync(ct);
            link.Server = await accepted;
        }
        link._request = new string('Q', characters);
        link._replyText = new string('R', characters);
        link.TelemetryText = "$" + new string('T', characters - 1);
        link.Telemetry = Encoding.ASCII.GetBytes(link.TelemetryText + "\n");
        link.Session = new KableSession<string>(new Factory(client), new AsciiLineCodec());
        await link.Session.StartAsync(ct);
        if (!stream) link._echo = link.EchoAsync(Encoding.ASCII.GetBytes(link._replyText + "\n"), ct);
        return link;
    }

    internal async Task QueryAsync()
    {
        string reply = await Session.RequestAsync<string>(_request, TimeSpan.FromSeconds(5));
        if (reply != _replyText) throw new InvalidDataException("Request/response mismatch.");
    }

    private async Task EchoAsync(byte[] reply, CancellationToken ct)
    {
        var codec = new AsciiLineCodec();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read = await Server.Input.ReadAsync(ct);
                var remaining = read.Buffer;
                while (codec.TryDecode(ref remaining, out _)) await Server.Output.WriteAsync(reply, ct);
                Server.Input.AdvanceTo(remaining.Start, remaining.End);
                if (read.IsCompleted) break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        await Session.DisposeAsync();
        await _echo;
        await Server.DisposeAsync();
        if (_listener != null) await _listener.DisposeAsync();
    }

    private sealed class Factory(IConnectionContext context) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(context);
    }
}
