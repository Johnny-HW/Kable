using System.Net;
using System.Text;
using DotNetty.Codecs;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
namespace Kable.Comparison.Adapters;

public sealed class DotNettyClient : IComparisonClient
{
    private static readonly object GroupLock = new();
    private static IEventLoopGroup? _sharedGroup;
    private static int _users;
    private readonly IEventLoopGroup _group;
    public DotNettyClient()
    {
        lock (GroupLock)
        {
            _group = _sharedGroup ??= new MultithreadEventLoopGroup(Math.Min(4, Environment.ProcessorCount));
            _users++;
        }
    }
    private readonly LineInbox _inbox = new();
    private IChannel? _channel;
    public async Task ConnectAsync(int port, CancellationToken ct)
    {
        _channel = await new Bootstrap().Group(_group).Channel<TcpSocketChannel>()
            .Option(ChannelOption.TcpNodelay, true)
            .Handler(new ActionChannelInitializer<ISocketChannel>(ch =>
            {
                ch.Pipeline.AddLast(new LineBasedFrameDecoder(65536));
                ch.Pipeline.AddLast(new StringDecoder(Encoding.ASCII));
                ch.Pipeline.AddLast(new StringEncoder(Encoding.ASCII));
                ch.Pipeline.AddLast(new Receiver(_inbox));
            })).ConnectAsync(new IPEndPoint(IPAddress.Loopback, port)).WaitAsync(ct);
    }
    public async Task<string> QueryAsync(string request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await _channel!.WriteAndFlushAsync(request + "\n").WaitAsync(ct);
        return await _inbox.ReadAsync(ct);
    }
    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.CloseAsync();
        bool shutdown;
        lock (GroupLock)
        {
            shutdown = --_users == 0;
            if (shutdown) _sharedGroup = null;
        }
        if (shutdown) await _group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.FromSeconds(2));
        _inbox.Close();
    }
    private sealed class Receiver(LineInbox inbox) : SimpleChannelInboundHandler<string>
    {
        protected override void ChannelRead0(IChannelHandlerContext context, string message) =>
            inbox.Put(message);
        public override void ChannelInactive(IChannelHandlerContext context) => inbox.Close();
        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            inbox.Close(exception);
            context.CloseAsync();
        }
    }
}
