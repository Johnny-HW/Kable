namespace Kable.Transports.Simulators;

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

/// <summary>Creates a fresh simulator per connection and transfers its lifetime to the context.</summary>
internal sealed class SimulatorConnectionFactory(Action<MockHardwareSimulator> configure) : IConnectionFactory
{
    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (client, server) = InMemoryConnectionContext.CreatePair();
        var simulator = new MockHardwareSimulator(server);
        try
        {
            configure(simulator);
            ct.ThrowIfCancellationRequested();
            simulator.Start();
            return new OwnedContext(client, simulator);
        }
        catch
        {
            await simulator.DisposeAsync().ConfigureAwait(false);
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class OwnedContext(InMemoryConnectionContext client, MockHardwareSimulator simulator) : IConnectionContext
    {
        private int _disposed;
        public string ConnectionId => client.ConnectionId;
        public string EndpointDescription => client.EndpointDescription;
        public PipeReader Input => client.Input;
        public PipeWriter Output => client.Output;
        public CancellationToken ConnectionClosed => client.ConnectionClosed;
        public void Abort(string reason) => client.Abort(reason);
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { await simulator.DisposeAsync().ConfigureAwait(false); }
            finally { await client.DisposeAsync().ConfigureAwait(false); }
        }
    }
}
