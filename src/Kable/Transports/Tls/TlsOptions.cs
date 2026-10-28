namespace Kable.Transports.Tls;

using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

public sealed class TlsClientOptions
{
    public TlsClientOptions(string targetHost) => TargetHost = targetHost;
    public string TargetHost { get; }
    public X509CertificateCollection ClientCertificates { get; } = new();
    public RemoteCertificateValidationCallback? RemoteCertificateValidation { get; set; }
    public SslProtocols Protocols { get; set; } = SslProtocols.None;
    public bool CheckCertificateRevocation { get; set; } = true;
}

public sealed class TlsServerOptions
{
    public TlsServerOptions(X509Certificate2 certificate) => Certificate = certificate;
    public X509Certificate2 Certificate { get; }
    public bool RequireClientCertificate { get; set; }
    public RemoteCertificateValidationCallback? RemoteCertificateValidation { get; set; }
    public SslProtocols Protocols { get; set; } = SslProtocols.None;
    public bool CheckCertificateRevocation { get; set; } = true;
}
