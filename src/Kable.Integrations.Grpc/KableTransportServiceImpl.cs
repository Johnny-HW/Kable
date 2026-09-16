namespace Kable.Integrations.Grpc;

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using global::Grpc.Core;
using global::Kable.Core;
using Kable.Integrations.Grpc.Protos;

/// <summary>
/// gRPC 서버 단에서 들어오는 클라이언트 스트림을 수신하여 IConnectionContext를 생성하고 채널로 전달하는 서비스 구현체
/// </summary>
public sealed class KableTransportServiceImpl : KableTransportService.KableTransportServiceBase, IConnectionListener
{
    private readonly Channel<IConnectionContext> _acceptedChannel;
    private readonly CancellationTokenSource _cts = new();

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
        var peer = context.Peer ?? "unknown-peer";
        var connectionCtx = new GrpcConnectionContext(requestStream, responseStream, $"grpc-server://{peer}");

        // 리스너 채널로 연결 컨텍스트 전달
        if (!await _acceptedChannel.Writer.WaitToWriteAsync(context.CancellationToken).ConfigureAwait(false))
        {
            connectionCtx.Abort("Server listener shut down.");
            return;
        }

        await _acceptedChannel.Writer.WriteAsync(connectionCtx, context.CancellationToken).ConfigureAwait(false);

        // 클라이언트 연결이 닫히거나 서버가 취소될 때까지 대기
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, _cts.Token, connectionCtx.ConnectionClosed);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using (linkedCts.Token.Register(() => tcs.TrySetResult(true)))
        {
            await tcs.Task.ConfigureAwait(false);
        }

        await connectionCtx.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask<IConnectionContext> AcceptAsync(CancellationToken ct = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        return await _acceptedChannel.Reader.ReadAsync(linkedCts.Token).ConfigureAwait(false);
    }

    public void Stop()
    {
        _cts.Cancel();
        _acceptedChannel.Writer.TryComplete();
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return default;
    }
}
