namespace Kable.Grpc;

using System;
using System.Threading;
using System.Threading.Tasks;
using global::Grpc.Net.Client;
using global::Kable.Core;
using Kable.Grpc.Protos;

/// <summary>
/// 원격 gRPC 엔드포인트에 접속하여 StreamTunnel을 열고 IConnectionContext를 반환하는 클라이언트 팩토리
/// </summary>
public sealed class GrpcClientFactory : IConnectionFactory
{
    private readonly string _serverAddress;
    private readonly GrpcChannelOptions? _channelOptions;

    public GrpcClientFactory(string serverAddress, GrpcChannelOptions? channelOptions = null)
    {
        _serverAddress = serverAddress ?? throw new ArgumentNullException(nameof(serverAddress));
        _channelOptions = channelOptions;
    }

    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var channel = _channelOptions != null
            ? GrpcChannel.ForAddress(_serverAddress, _channelOptions)
            : GrpcChannel.ForAddress(_serverAddress);

        IDisposable? callDisposable = null;
        try
        {
            var client = new KableTransportService.KableTransportServiceClient(channel);
            // Stream lifetime is owned by GrpcConnectionContext/ChannelCallScope, not the transient connect token.
            var call = client.StreamTunnel(cancellationToken: CancellationToken.None);
            callDisposable = call;

            var ctx = new GrpcConnectionContext(
                call.ResponseStream,
                call.RequestStream,
                $"grpc://{_serverAddress}",
                new ChannelCallScope(channel, call));

            return new ValueTask<IConnectionContext>(ctx);
        }
        catch
        {
            callDisposable?.Dispose();
            channel.Dispose();
            throw;
        }
    }

    private sealed class ChannelCallScope : IDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly IDisposable _call;

        public ChannelCallScope(GrpcChannel channel, IDisposable call)
        {
            _channel = channel;
            _call = call;
        }

        public void Dispose()
        {
            try
            {
                _call.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                System.Diagnostics.Debug.WriteLine($"[GrpcClientFactory] Call dispose error: {ex.Message}");
            }

            try
            {
                _channel.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                System.Diagnostics.Debug.WriteLine($"[GrpcClientFactory] Channel dispose error: {ex.Message}");
            }
        }
    }
}
