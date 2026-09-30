namespace Kable.Benchmarks;

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;

[MemoryDiagnoser]
public class SessionBenchmarks
{
    private LoopbackConnectionContext _connContext = null!;
    private KableSession<string> _session = null!;
    private CancellationTokenSource _cts = null!;
    private Task _echoServerTask = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cts = new CancellationTokenSource();
        _connContext = new LoopbackConnectionContext();
        var factory = new SimpleFactory(_connContext);
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        _session = new KableSession<string>(factory, codec);
        _session.StartAsync().AsTask().GetAwaiter().GetResult();

        // Echo server for request-response
        _echoServerTask = Task.Run(async () =>
        {
            var reader = _connContext.RemoteRead;
            var writer = _connContext.RemoteWrite;
            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(token);
                var buffer = result.Buffer;

                while (TryReadLine(ref buffer, out var line))
                {
                    if (line.StartsWith("REQ"))
                    {
                        var reply = Encoding.ASCII.GetBytes("RSP:OK\n");
                        await writer.WriteAsync(reply, token);
                        await writer.FlushAsync(token);
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted || result.IsCanceled) break;
            }
        });
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _cts.Cancel();
        _session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { _echoServerTask.Wait(500); } catch { }
        _cts.Dispose();
    }

    [Benchmark(Description = "KableSession.RequestAsync (RoundTrip Latency & Alloc)")]
    public async Task<string> RequestResponse_RoundTrip()
    {
        return await _session.RequestAsync<string>("REQ_PING", TimeSpan.FromSeconds(2));
    }

    [Benchmark(Description = "KableSession.Streaming (100 Inbound Unsolicited Messages)")]
    public async Task<int> InboundStreaming_Burst100()
    {
        var raw = Encoding.ASCII.GetBytes("$DATA,001,VAL=12.34\n");
        var writer = _connContext.RemoteWrite;

        for (int i = 0; i < 100; i++)
        {
            await writer.WriteAsync(raw);
        }
        await writer.FlushAsync();

        int count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var item in _session.GetStreamAsync(cts.Token))
        {
            count++;
            if (count == 100) break;
        }

        return count;
    }

    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out string line)
    {
        var pos = buffer.PositionOf((byte)0x0A);
        if (pos == null)
        {
            line = string.Empty;
            return false;
        }

        var slice = buffer.Slice(0, pos.Value);
        line = Encoding.ASCII.GetString(slice.ToArray());
        buffer = buffer.Slice(buffer.GetPosition(1, pos.Value));
        return true;
    }

    private sealed class LoopbackConnectionContext : IConnectionContext
    {
        private readonly Pipe _inPipe = new();
        private readonly Pipe _outPipe = new();
        private readonly CancellationTokenSource _cts = new();

        public string ConnectionId => "LOOPBACK";
        public string EndpointDescription => "In-Memory Loopback";
        public PipeReader Input => _inPipe.Reader;
        public PipeWriter Output => _outPipe.Writer;
        public CancellationToken ConnectionClosed => _cts.Token;

        public PipeReader RemoteRead => _outPipe.Reader;
        public PipeWriter RemoteWrite => _inPipe.Writer;

        public void Abort(string reason) => _cts.Cancel();

        public ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _inPipe.Reader.Complete();
            _inPipe.Writer.Complete();
            _outPipe.Reader.Complete();
            _outPipe.Writer.Complete();
            return default;
        }
    }

    private sealed class SimpleFactory : IConnectionFactory
    {
        private readonly IConnectionContext _ctx;
        public SimpleFactory(IConnectionContext ctx) => _ctx = ctx;
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct) => new(_ctx);
    }
}
