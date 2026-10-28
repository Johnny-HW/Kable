namespace Kable.Tests.Fixtures;

using System;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

/// <summary>
/// 1:N 서버 단위 테스트를 위한 순수 인메모리 양방향 파이프 연결 쌍
/// </summary>
public static class TestDuplexPipePair
{
    public static (IConnectionContext ServerSide, TestClientConnection ClientSide) Create(
        string? connectionId = null,
        PipeOptions? clientToServerOptions = null,
        PipeOptions? serverToClientOptions = null)
    {
        var id = connectionId ?? ("CONN-" + Guid.NewGuid().ToString("N")[..8]);
        var pipeC2S = new Pipe(clientToServerOptions ?? new PipeOptions());
        var pipeS2C = new Pipe(serverToClientOptions ?? new PipeOptions());

        var serverCts = new CancellationTokenSource();
        var clientCts = new CancellationTokenSource();

        var serverSide = new MemoryConnectionEndpoint(
            id,
            "Memory-ServerSide",
            pipeC2S.Reader,
            pipeS2C.Writer,
            serverCts);

        var clientSide = new TestClientConnection(
            id,
            pipeS2C.Reader,
            pipeC2S.Writer,
            clientCts);

        return (serverSide, clientSide);
    }

    private sealed class MemoryConnectionEndpoint : IConnectionContext
    {
        private readonly CancellationTokenSource _cts;
        private int _isDisposed;

        public string ConnectionId { get; }
        public string EndpointDescription { get; }
        public PipeReader Input { get; }
        public PipeWriter Output { get; }
        public CancellationToken ConnectionClosed => _cts.Token;

        public MemoryConnectionEndpoint(
            string connectionId,
            string endpointDescription,
            PipeReader input,
            PipeWriter output,
            CancellationTokenSource cts)
        {
            ConnectionId = connectionId;
            EndpointDescription = endpointDescription;
            Input = input;
            Output = output;
            _cts = cts;
        }

        public void Abort(string reason)
        {
            _cts.Cancel();
            Input.CancelPendingRead();
            Output.CancelPendingFlush();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                _cts.Cancel();
                await Input.CompleteAsync().ConfigureAwait(false);
                await Output.CompleteAsync().ConfigureAwait(false);
            }
        }
    }

    public sealed class TestClientConnection : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts;
        private int _isDisposed;

        public string ConnectionId { get; }
        public PipeReader Input { get; }
        public PipeWriter Output { get; }
        public CancellationToken ConnectionClosed => _cts.Token;

        public TestClientConnection(
            string connectionId,
            PipeReader input,
            PipeWriter output,
            CancellationTokenSource cts)
        {
            ConnectionId = connectionId;
            Input = input;
            Output = output;
            _cts = cts;
        }

        public async Task WriteAsciiLineAsync(string text, byte delimiter = 0x0A)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await Output.WriteAsync(new ReadOnlyMemory<byte>(bytes)).ConfigureAwait(false);
            await Output.WriteAsync(new ReadOnlyMemory<byte>(new[] { delimiter })).ConfigureAwait(false);
            await Output.FlushAsync().ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                _cts.Cancel();
                await Output.CompleteAsync().ConfigureAwait(false);
                await Input.CompleteAsync().ConfigureAwait(false);
            }
        }
    }
}
