namespace Kable.Builder;

using System;
using System.Net;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Transports;

/// <summary>
/// 1:N 고수준 KableServer 구성을 위한 플루언트 빌더
/// </summary>
public sealed class KableServerBuilder<TMessage>
{
    private IConnectionListener? _listener;
    private Func<IProtocolCodec<TMessage>>? _codecFactory;
    private readonly KableServerOptions _options = new();
    private IPAddress? _tcpAddress;
    private int _tcpPort;
    private bool _built;

    public KableServerBuilder<TMessage> ListenTcp(IPAddress address, int port)
    {
        if (address == null) throw new ArgumentNullException(nameof(address));
        if (port < 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        _tcpAddress = address;
        _tcpPort = port;
        _listener = null;
        return this;
    }

    public KableServerBuilder<TMessage> ListenTcp(int port)
    {
        return ListenTcp(IPAddress.Any, port);
    }

    public KableServerBuilder<TMessage> WithListener(IConnectionListener listener)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _tcpAddress = null;
        return this;
    }

    public KableServerBuilder<TMessage> UseCodec(Func<IProtocolCodec<TMessage>> codecFactory)
    {
        _codecFactory = codecFactory ?? throw new ArgumentNullException(nameof(codecFactory));
        return this;
    }

    public KableServerBuilder<TMessage> ConfigureOptions(Action<KableServerOptions> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        configure(_options);
        return this;
    }

    public IKableServer<TMessage> Build()
    {
        if (_built) throw new InvalidOperationException("A builder can build only one server.");
        if (_listener == null && _tcpAddress == null)
        {
            throw new InvalidOperationException("No listener configured. Call ListenTcp or WithListener.");
        }

        if (_codecFactory == null)
        {
            throw new InvalidOperationException("No codec factory configured. Call UseCodec.");
        }

        var options = _options.Snapshot();
        var listener = _listener ?? new TcpConnectionListener(_tcpAddress!, _tcpPort);
        var server = new KableServer<TMessage>(listener, _codecFactory, options);
        _built = true;
        return server;
    }
}
