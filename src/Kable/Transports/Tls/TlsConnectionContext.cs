namespace Kable.Transports.Tls;

using System;
using System.IO.Pipelines;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

/// <summary>Owns the accepted/connected TCP client and authenticated stream, but not certificates.</summary>
public sealed class TlsConnectionContext : IConnectionContext
{
    private readonly TcpClient _client;
    private readonly SslStream _stream;
    private readonly CancellationTokenSource _closed = new();
    private readonly object _sync = new();
    private Task? _disposal;
    private int _aborted;
    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");
    public string EndpointDescription { get; }
    public PipeReader Input { get; }
    public PipeWriter Output { get; }
    public CancellationToken ConnectionClosed { get; }

    internal TlsConnectionContext(TcpClient client, SslStream stream)
    {
        _client = client;
        _stream = stream;
        ConnectionClosed = _closed.Token;
        EndpointDescription = "TLS " + client.Client.RemoteEndPoint;
        Input = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
        Output = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
    }

    public static async Task<TlsConnectionContext> AcceptAsync(TcpClient client, TlsServerOptions options, CancellationToken ct = default)
    {
        SslStream? stream = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            stream = new SslStream(client.GetStream(), false, options.RemoteCertificateValidation);
            using (ct.Register(client.Dispose))
            {
#if NETSTANDARD2_0
                await stream.AuthenticateAsServerAsync(options.Certificate, options.RequireClientCertificate,
                    options.Protocols, options.CheckCertificateRevocation).ConfigureAwait(false);
#else
                await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = options.Certificate, ClientCertificateRequired = options.RequireClientCertificate,
                    EnabledSslProtocols = options.Protocols,
                    CertificateRevocationCheckMode = options.CheckCertificateRevocation
                        ? System.Security.Cryptography.X509Certificates.X509RevocationMode.Online
                        : System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
                }, ct).ConfigureAwait(false);
#endif
            }
            ct.ThrowIfCancellationRequested();
            return new(client, stream);
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            stream?.Dispose(); client.Dispose(); throw new OperationCanceledException(ct);
        }
        catch { stream?.Dispose(); client.Dispose(); throw; }
    }

    public void Abort(string reason)
    {
        if (Interlocked.Exchange(ref _aborted, 1) != 0) return;
        _closed.Cancel();
        _client.Dispose();
        Input.CancelPendingRead();
        Output.CancelPendingFlush();
    }
    public ValueTask DisposeAsync()
    {
        lock (_sync) return new ValueTask(_disposal ??= DisposeCoreAsync());
    }
    private async Task DisposeCoreAsync()
    {
        Abort("Disposed");
        try { await Input.CompleteAsync().ConfigureAwait(false); }
        finally
        {
            try { await Output.CompleteAsync().ConfigureAwait(false); }
            finally { _stream.Dispose(); _closed.Dispose(); }
        }
    }
}
