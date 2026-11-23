namespace Kable.Grpc;

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using global::Grpc.Core;
using global::Kable.Core;
using Kable.Grpc.Protos;

/// <summary>
/// gRPC 서버 단에서 들어오는 클라이언트 스트림을 수신하여 IConnectionContext를 생성하고 채널로 전달하는 서비스 구현체
/// </summary>
public sealed class KableTransportServiceImpl : KableTransportService.KableTransportServiceBase, IConnectionListener
{
    private readonly Channel<IConnectionContext> _acceptedChannel;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _sync = new();
    private bool _stopped;
    private int _disposed;

    public KableTransportServiceImpl(int channelCapacity = 64)
    {
        _acceptedChannel = Channel.CreateBounded<IConnectionContext>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public override async Task StreamTunnel(
        IAsyncStreamReader<KablePacket> requestStream,
        IServerStreamWriter<KablePacket> responseStream,
        ServerCallContext context)
    {
        CancellationToken serverToken;
        lock (_sync)
        {
            if (_stopped) return;
            serverToken = _cts.Token;
        }

        var peer = context.Peer ?? "unknown-peer";
        var connectionCtx = new GrpcConnectionContext(requestStream, responseStream, $"grpc-server://{peer}");
        try
        {
            using var acceptCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, serverToken);
            if (!await _acceptedChannel.Writer.WaitToWriteAsync(acceptCts.Token).ConfigureAwait(false))
            {
                connectionCtx.Abort("Server listener shut down.");
                return;
            }

            await _acceptedChannel.Writer.WriteAsync(connectionCtx, acceptCts.Token).ConfigureAwait(false);

            // 클라이언트 연결이 닫히거나 서버가 취소될 때까지 대기
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, serverToken, connectionCtx.ConnectionClosed);
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (linkedCts.Token.Register(() => tcs.TrySetResult(true)))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            connectionCtx.Abort("Stream tunnel cancelled.");
        }
        catch (ChannelClosedException)
        {
            connectionCtx.Abort("Server listener shut down.");
        }
        finally
        {
            await connectionCtx.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask<IConnectionContext> AcceptAsync(CancellationToken ct = default)
    {
        CancellationToken serverToken;
        lock (_sync)
        {
            if (_stopped) throw new OperationCanceledException("Listener stopped.");
            serverToken = _cts.Token;
        }
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, serverToken);
        return await _acceptedChannel.Reader.ReadAsync(linkedCts.Token).ConfigureAwait(false);
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_stopped) return;
            _stopped = true;
            _cts.Cancel();
            _acceptedChannel.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Stop();
        while (_acceptedChannel.Reader.TryRead(out var unaccepted))
        {
            try { await unaccepted.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                System.Diagnostics.Debug.WriteLine($"[KableTransportServiceImpl] Unaccepted context dispose failed: {ex.Message}");
            }
        }
        lock (_sync)
        {
            _cts.Dispose();
        }
    }
}
