namespace Kable.Transports;

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

/// <summary>
/// 네트워크 소켓이나 직렬 포트 없이, 두 개의 크로스 파이프라인(Pipe)을 연결하여
/// Zero-Allocation / Zero-Network 인메모리 루프백을 제공하는 연결 컨텍스트.
/// </summary>
public sealed class InMemoryConnectionContext : IConnectionContext
{
    private readonly Pipe _inboundPipe;
    private readonly Pipe _outboundPipe;
    private readonly CancellationTokenSource _cts = new();
    private int _isDisposed;

    public string ConnectionId { get; }
    public string EndpointDescription { get; }
    public PipeReader Input => _inboundPipe.Reader;
    public PipeWriter Output => _outboundPipe.Writer;
    public CancellationToken ConnectionClosed => _cts.Token;

    /// <summary>
    /// 시뮬레이터/상대방 입장에서 읽고 쓰는 페어(Pair) 컨텍스트
    /// </summary>
    public InMemoryConnectionContext Peer { get; }

    private InMemoryConnectionContext(
        Pipe inboundPipe, 
        Pipe outboundPipe, 
        string connectionId, 
        string endpointDescription,
        InMemoryConnectionContext? peer)
    {
        _inboundPipe = inboundPipe;
        _outboundPipe = outboundPipe;
        ConnectionId = connectionId;
        EndpointDescription = endpointDescription;

        if (peer != null)
        {
            Peer = peer;
        }
        else
        {
            // 교차 연결된 상대방(Peer) 컨텍스트 생성
            Peer = new InMemoryConnectionContext(
                inboundPipe: outboundPipe,
                outboundPipe: inboundPipe,
                connectionId: Guid.NewGuid().ToString("N"),
                endpointDescription: "In-Memory Simulator Peer",
                peer: this);
        }
    }

    /// <summary>
    /// 클라이언트용과 시뮬레이터(Peer)용이 크로스 연결된 인메모리 컨텍스트 쌍을 생성합니다.
    /// </summary>
    public static (InMemoryConnectionContext Client, InMemoryConnectionContext Server) CreatePair(
        string clientEndpoint = "In-Memory Client",
        string serverEndpoint = "In-Memory Server",
        PipeOptions? pipeOptions = null)
    {
        var options = pipeOptions ?? new PipeOptions(pauseWriterThreshold: 1024 * 1024, resumeWriterThreshold: 512 * 1024);
        var pipe1 = new Pipe(options);
        var pipe2 = new Pipe(options);

        var client = new InMemoryConnectionContext(
            inboundPipe: pipe1,
            outboundPipe: pipe2,
            connectionId: Guid.NewGuid().ToString("N"),
            endpointDescription: clientEndpoint,
            peer: null);

        return (client, client.Peer);
    }

    public void Abort(string reason)
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _cts.Cancel();
            _inboundPipe.Reader.CancelPendingRead();
            _outboundPipe.Writer.CancelPendingFlush();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Abort("Disposed");
        await Task.CompletedTask;
    }
}
