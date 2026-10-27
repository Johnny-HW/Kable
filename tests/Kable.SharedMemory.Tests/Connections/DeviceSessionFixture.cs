namespace Kable.SharedMemory.Tests.Connections;

using System.Text;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Transports;

internal sealed class DeviceSessionFixture : IAsyncDisposable
{
    private readonly IConnectionContext _peer;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _echo;
    private readonly Factory _factory;
    private int _executions;
    internal readonly TaskCompletionSource CommandStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource CompleteCommand = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal KableSession<string> Session { get; }
    internal int ConnectCount => _factory.Count;
    internal int Executions => Volatile.Read(ref _executions);

    private DeviceSessionFixture(IConnectionContext client, IConnectionContext peer)
    {
        _peer = peer;
        _factory = new Factory(client);
        Session = new KableSession<string>(_factory, new AsciiLineCodec());
        _echo = EchoAsync();
    }

    internal static async Task<DeviceSessionFixture> CreateAsync()
    {
        var (client, peer) = InMemoryConnectionContext.CreatePair();
        var fixture = new DeviceSessionFixture(client, peer);
        await fixture.Session.StartAsync();
        return fixture;
    }

    private async Task EchoAsync()
    {
        var codec = new AsciiLineCodec();
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var read = await _peer.Input.ReadAsync(_stop.Token);
                var remaining = read.Buffer;
                while (codec.TryDecode(ref remaining, out string command))
                {
                    if (command == "EXEC")
                    {
                        Interlocked.Increment(ref _executions);
                        CommandStarted.TrySetResult();
                        await CompleteCommand.Task.WaitAsync(_stop.Token);
                    }
                    await _peer.Output.WriteAsync(Encoding.ASCII.GetBytes(command == "EXEC" ? "DONE\n" : "READY\n"), _stop.Token);
                }
                _peer.Input.AdvanceTo(remaining.Start, remaining.End);
                if (read.IsCompleted) break;
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        await Session.DisposeAsync();
        await _echo;
        await _peer.DisposeAsync();
        _stop.Dispose();
    }

    private sealed class Factory(IConnectionContext context) : IConnectionFactory
    {
        internal int Count;
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref Count);
            return new(context);
        }
    }
}
