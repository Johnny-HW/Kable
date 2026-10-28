namespace Kable.Transports;

using System;
using System.IO.Pipes;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

public sealed class NamedPipeConnectionContext : IConnectionContext
{
    private readonly NamedPipeClientStream _pipeStream;
    private readonly Lifecycle.StreamConnectionLifetime _lifetime;

    public string ConnectionId { get; }
    public string EndpointDescription { get; }
    public PipeReader Input { get; }
    public PipeWriter Output { get; }
    public CancellationToken ConnectionClosed => _lifetime.Closed;

    public NamedPipeConnectionContext(NamedPipeClientStream pipeStream, string pipeName)
    {
        _pipeStream = pipeStream;
        ConnectionId = Guid.NewGuid().ToString("N");
        EndpointDescription = $"NamedPipe://localhost/{pipeName}";

        Input = PipeReader.Create(pipeStream, new StreamPipeReaderOptions(leaveOpen: true));
        Output = PipeWriter.Create(pipeStream, new StreamPipeWriterOptions(leaveOpen: true));
        _lifetime = new Lifecycle.StreamConnectionLifetime(_pipeStream, Input, Output, _pipeStream.Close);
    }

    public void Abort(string reason) => _lifetime.Abort();
    public ValueTask DisposeAsync() => _lifetime.DisposeAsync();

}

public sealed class NamedPipeConnectionFactory : IConnectionFactory
{
    private readonly string _pipeName;
    private readonly string _serverName;
    private readonly int _timeoutMs;

    public NamedPipeConnectionFactory(string pipeName, string serverName = ".", int timeoutMs = 5000)
    {
        _pipeName = pipeName;
        _serverName = serverName;
        _timeoutMs = timeoutMs;
    }

    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        var pipeStream = new NamedPipeClientStream(
            _serverName,
            _pipeName,
            PipeDirection.InOut,
            System.IO.Pipes.PipeOptions.Asynchronous);

#if NETSTANDARD2_0
        pipeStream.Connect(_timeoutMs);
        await Task.CompletedTask;
#else
        await pipeStream.ConnectAsync(_timeoutMs, ct).ConfigureAwait(false);
#endif

        return new NamedPipeConnectionContext(pipeStream, _pipeName);
    }
}
