namespace Kable.Transports;

using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

public sealed class TcpConnectionContext : IConnectionContext
{
    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly Lifecycle.StreamConnectionLifetime _lifetime;

    public string ConnectionId { get; }
    public string EndpointDescription { get; }
    public PipeReader Input { get; }
    public PipeWriter Output { get; }
    public CancellationToken ConnectionClosed => _lifetime.Closed;

    public TcpConnectionContext(Socket socket)
    {
        _socket = socket;
        ConnectionId = Guid.NewGuid().ToString("N");
        EndpointDescription = socket.RemoteEndPoint?.ToString() ?? "TCP Unknown";

        _stream = new NetworkStream(socket, ownsSocket: false);
        Input = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
        Output = PipeWriter.Create(_stream, new StreamPipeWriterOptions(leaveOpen: true));
        _lifetime = new Lifecycle.StreamConnectionLifetime(_stream, Input, Output, _socket.Dispose);
    }

    public void Abort(string reason) => _lifetime.Abort();
    public ValueTask DisposeAsync() => _lifetime.DisposeAsync();

}

public sealed class TcpConnectionFactory : IConnectionFactory
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _connectTimeoutMs;

    public TcpConnectionFactory(string host, int port, int connectTimeoutMs = 0)
    {
        _host = host;
        _port = port;
        _connectTimeoutMs = connectTimeoutMs;
    }

    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        CancellationTokenSource? timeoutCts = null;
        CancellationTokenSource? linkedCts = null;
        var effectiveCt = ct;

        if (_connectTimeoutMs > 0)
        {
            timeoutCts = new CancellationTokenSource(_connectTimeoutMs);
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            effectiveCt = linkedCts.Token;
        }

        try
        {
#if NETSTANDARD2_0
            var connectTask = Task.Factory.FromAsync(
                socket.BeginConnect(_host, _port, null, null),
                socket.EndConnect);

            if (effectiveCt.CanBeCanceled)
            {
                using (effectiveCt.Register(() => { try { socket.Dispose(); } catch { } }))
                {
                    await connectTask.ConfigureAwait(false);
                }
            }
            else
            {
                await connectTask.ConfigureAwait(false);
            }
#else
            await socket.ConnectAsync(_host, _port, effectiveCt).ConfigureAwait(false);
#endif
            return new TcpConnectionContext(socket);
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !ct.IsCancellationRequested)
        {
            socket.Dispose();
            throw new TimeoutException($"TCP connection to {_host}:{_port} timed out after {_connectTimeoutMs}ms.");
        }
        catch
        {
            socket.Dispose();
            throw;
        }
        finally
        {
            linkedCts?.Dispose();
            timeoutCts?.Dispose();
        }
    }
}
