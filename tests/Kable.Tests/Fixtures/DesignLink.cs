namespace Kable.Tests.Fixtures;

using System.Buffers;
using System.Text;
using System.Threading.Channels;
using Kable.Core;
using Kable.Observability;
using Kable.Transports;

internal sealed class DesignLink : IConnectionFactory
{
    private readonly InMemoryConnectionContext _client;
    internal InMemoryConnectionContext Peer { get; }
    internal DesignLink() { (_client, Peer) = InMemoryConnectionContext.CreatePair(); }
    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(_client);
    internal async Task ReadRequestAsync(CancellationToken ct)
    {
        var read = await Peer.Input.ReadAsync(ct);
        Peer.Input.AdvanceTo(read.Buffer.End);
    }
    internal async Task SendAsync(string text, CancellationToken ct) =>
        await Peer.Output.WriteAsync(Encoding.ASCII.GetBytes(text), ct);
}

internal sealed class TraceSignal : ICommObserver
{
    internal TaskCompletionSource<string> Streamed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<string> Fault { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        if (trace.Tag == "STREAM") Streamed.TrySetResult(trace.ParsedText ?? "");
        if (trace.Tag == "READ_LOOP_FAULT") Fault.TrySetResult(trace.ParsedText ?? "");
    }
    public ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();
}
