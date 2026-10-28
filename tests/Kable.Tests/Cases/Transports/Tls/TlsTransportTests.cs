namespace Kable.Tests.Cases.Transports.Tls;

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kable.Transports.Tls;
using Xunit;

public sealed class TlsTransportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EncryptedDuplexAndMutualAuthentication(bool mutual)
    {
        using var certificate = Certificate("localhost");
        using var clientCertificate = Certificate("client");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            var tcp = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var connection = await TlsConnectionContext.AcceptAsync(tcp, new TlsServerOptions(certificate)
            {
                RequireClientCertificate = mutual,
                RemoteCertificateValidation = mutual ? (_, cert, _, _) => cert?.GetCertHashString() == clientCertificate.GetCertHashString() : null
            }, timeout.Token);
            var read = await connection.Input.ReadAsync(timeout.Token);
            Assert.Equal(42, read.Buffer.FirstSpan[0]);
            connection.Input.AdvanceTo(read.Buffer.End);
            await connection.Output.WriteAsync(new byte[] { 99 }, timeout.Token);
        });
        var options = new TlsClientOptions("localhost")
        {
            RemoteCertificateValidation = (_, cert, _, errors) => cert?.GetCertHashString() == certificate.GetCertHashString()
                && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == 0
        };
        if (mutual) options.ClientCertificates.Add(clientCertificate);
        await using var client = await new TlsTcpConnectionFactory("127.0.0.1", port, options).ConnectAsync(timeout.Token);
        await client.Output.WriteAsync(new byte[] { 42 }, timeout.Token);
        var reply = await client.Input.ReadAsync(timeout.Token);
        Assert.Equal(99, reply.Buffer.FirstSpan[0]);
        client.Input.AdvanceTo(reply.Buffer.End);
        client.Abort("done");
        await client.DisposeAsync();
        await server;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UntrustedOrWrongHostIsRejected(bool wrongHost)
    {
        using var certificate = Certificate("localhost");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = Task.Run(async () =>
        {
            var tcp = await listener.AcceptTcpClientAsync(timeout.Token);
            var error = await Record.ExceptionAsync(async () =>
            {
                await using var connection = await TlsConnectionContext.AcceptAsync(tcp, new TlsServerOptions(certificate), timeout.Token);
            });
            Assert.True(error == null || error is AuthenticationException or IOException);
        });
        var options = new TlsClientOptions(wrongHost ? "wrong.example" : "localhost");
        if (wrongHost) options.RemoteCertificateValidation = (_, _, _, errors) =>
            (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == 0;
        var factory = new TlsTcpConnectionFactory("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, options);
        await Assert.ThrowsAsync<AuthenticationException>(() => factory.ConnectAsync(timeout.Token).AsTask());
        await server;
    }

    [Fact]
    public async Task HandshakeCancellationClosesSocket()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var cancel = new CancellationTokenSource();
        var connecting = new TlsTcpConnectionFactory("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            new TlsClientOptions("localhost")).ConnectAsync(cancel.Token).AsTask();
        using var peer = await listener.AcceptTcpClientAsync();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    private static X509Certificate2 Certificate(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName(name);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }
}
