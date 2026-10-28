using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Kable.OpcUa;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

internal static class OpcUaProbe
{
    internal static async Task RunAsync(CancellationToken ct)
    {
        using var picker = new TcpListener(IPAddress.Loopback, 0);
        picker.Start(); int port = ((IPEndPoint)picker.LocalEndpoint).Port; picker.Stop();
        string root = Path.Combine(Path.GetTempPath(), "Kable-Package-Probe", Guid.NewGuid().ToString("N"));
        string endpoint = $"opc.tcp://localhost:{port}/ReleaseProbe";
        var configuration = new ApplicationConfiguration
        {
            ApplicationName = "KablePackageServer", ApplicationUri = "urn:localhost:KablePackageServer", ApplicationType = ApplicationType.Server,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier { StoreType = "Directory", StorePath = Path.Combine(root, "server", "own"), SubjectName = "CN=KablePackageServer" },
                TrustedPeerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(root, "server", "trusted") },
                TrustedIssuerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(root, "server", "issuer") },
                RejectedCertificateStore = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(root, "server", "rejected") },
                AutoAcceptUntrustedCertificates = true
            },
            TransportQuotas = new TransportQuotas { OperationTimeout = 5000 },
            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = new StringCollection { endpoint },
                SecurityPolicies = new ServerSecurityPolicyCollection { new ServerSecurityPolicy
                { SecurityMode = MessageSecurityMode.SignAndEncrypt, SecurityPolicyUri = SecurityPolicies.Basic256Sha256 } },
                UserTokenPolicies = new UserTokenPolicyCollection { new UserTokenPolicy(UserTokenType.Anonymous) },
                DiagnosticsEnabled = false
            }
        };
        await configuration.ValidateAsync(ApplicationType.Server);
#pragma warning disable CS0618
        var application = new ApplicationInstance { ApplicationConfiguration = configuration };
        await application.CheckApplicationInstanceCertificatesAsync(true, null, ct);
        using var server = new StandardServer();
        await application.StartAsync(server);
#pragma warning restore CS0618
        try
        {
            string clientRoot = Path.Combine(root, "client");
            string trust = Path.Combine(clientRoot, "trusted", "certs");
            Directory.CreateDirectory(trust);
            var certificate = configuration.SecurityConfiguration.ApplicationCertificate.Certificate;
            File.WriteAllBytes(Path.Combine(trust, certificate.Thumbprint + ".der"), certificate.Export(X509ContentType.Cert));
            await using var bridge = new OpcUaClientBridge(new OpcUaOptions { EndpointUrl = endpoint, PkiRootPath = clientRoot });
            await bridge.ConnectAsync(ct);
            string node = VariableIds.Server_ServerStatus_CurrentTime.ToString();
            var value = await bridge.ReadNodeValueAsync(node, ct);
            if (!value.IsGood || value.Value is not DateTime) throw new Exception("Packaged secure OPC UA read failed.");
            await using var subscription = await bridge.SubscribeAsync(new[] { node }, ct: ct);
            if (!(await subscription.Values.ReadAsync(ct)).IsGood) throw new Exception("Packaged OPC UA subscription failed.");
        }
        finally { await application.StopAsync(); }
    }
}
