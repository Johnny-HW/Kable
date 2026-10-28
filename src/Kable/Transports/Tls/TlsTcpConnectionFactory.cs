namespace Kable.Transports.Tls;

using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

public sealed class TlsTcpConnectionFactory : IConnectionFactory
{
    private readonly string _host;
    private readonly int _port;
    private readonly TlsClientOptions _options;
    private readonly int _timeoutMs;
    public TlsTcpConnectionFactory(string host, int port, TlsClientOptions options, int connectTimeoutMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
        if (options == null || string.IsNullOrWhiteSpace(options.TargetHost)) throw new ArgumentException("TLS target host is required.", nameof(options));
        if (connectTimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(connectTimeoutMs));
        _host = host; _port = port; _options = options; _timeoutMs = connectTimeoutMs;
    }

    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        using var deadline = new CancellationTokenSource(_timeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var token = linked.Token;
        var client = new TcpClient { NoDelay = true };
        SslStream? stream = null;
        try
        {
            token.ThrowIfCancellationRequested();
            using (token.Register(client.Dispose))
            {
#if NETSTANDARD2_0
                await client.ConnectAsync(_host, _port).ConfigureAwait(false);
#else
                await client.ConnectAsync(_host, _port, token).ConfigureAwait(false);
#endif
                stream = new SslStream(client.GetStream(), false, _options.RemoteCertificateValidation);
#if NETSTANDARD2_0
                await stream.AuthenticateAsClientAsync(_options.TargetHost, _options.ClientCertificates,
                    _options.Protocols, _options.CheckCertificateRevocation).ConfigureAwait(false);
#else
                await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = _options.TargetHost, ClientCertificates = _options.ClientCertificates,
                    EnabledSslProtocols = _options.Protocols,
                    CertificateRevocationCheckMode = _options.CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck
                }, token).ConfigureAwait(false);
#endif
            }
            token.ThrowIfCancellationRequested();
            return new TlsConnectionContext(client, stream);
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            stream?.Dispose(); client.Dispose();
            if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
            throw new TimeoutException("TCP/TLS connection or handshake timed out.");
        }
        catch { stream?.Dispose(); client.Dispose(); throw; }
    }
}
