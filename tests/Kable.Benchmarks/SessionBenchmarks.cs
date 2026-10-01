namespace Kable.Benchmarks;

using System.IO.Pipelines;
using System.Text;
using BenchmarkDotNet.Attributes;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Observability;
using Kable.Transports;

[MemoryDiagnoser]
public class SessionBenchmarks
{
    [Params(32, 1024)] public int Characters { get; set; }
    [Params(false, true)] public bool Observe { get; set; }
    private KableSession<string> _session = null!;
    private IConnectionContext _server = null!;
    private CancellationTokenSource _stop = null!;
    private Task _echo = null!;
    private string _request = null!;
    private byte[] _reply = null!;
    private byte[] _telemetry = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _stop = new CancellationTokenSource();
        var (client, server) = InMemoryConnectionContext.CreatePair();
        _server = server;
        _request = new string('Q', Characters);
        _reply = Encoding.ASCII.GetBytes(new string('R', Characters) + "\n");
        _telemetry = Encoding.ASCII.GetBytes("$" + new string('T', Characters - 1) + "\n");
        _session = new KableSession<string>(new Factory(client), new AsciiLineCodec(),
            observer: Observe ? new CommObserver() : null);
        await _session.StartAsync();
        _echo = EchoAsync();
    }

    private async Task EchoAsync()
    {
        var codec = new AsciiLineCodec();
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var result = await _server.Input.ReadAsync(_stop.Token);
                var remaining = result.Buffer;
                while (codec.TryDecode(ref remaining, out _))
                    await _server.Output.WriteAsync(_reply, _stop.Token);
                _server.Input.AdvanceTo(remaining.Start, remaining.End);
                if (result.IsCompleted) break;
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    [Benchmark]
    public async Task<string> RequestResponse()
        => await _session.RequestAsync<string>(_request, TimeSpan.FromSeconds(5));

    [Benchmark(OperationsPerInvoke = 100)]
    public async Task<int> Stream100()
    {
        for (int i = 0; i < 100; i++) await _server.Output.WriteAsync(_telemetry);
        int count = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var message in _session.GetStreamAsync(timeout.Token))
            if (++count == 100) break;
        return count;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _stop.Cancel();
        await _echo;
        await _session.DisposeAsync();
        await _server.DisposeAsync();
        _stop.Dispose();
    }

    private sealed class Factory(IConnectionContext context) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(context);
    }
}
