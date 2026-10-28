namespace Kable.OpcUa;

using System.Text;
using Opc.Ua;

public sealed partial class OpcUaClientBridge
{
    private UserIdentity? _identity;
    private byte[]? _credentialBytes;
    private static async Task<ApplicationConfiguration> CreateSecurityConfigurationAsync(OpcUaOptions options)
    {
        var config = new ApplicationConfiguration
        {
            ApplicationName = "Kable.OpcUa.Client",
            ApplicationUri = Utils.Format("urn:{0}:Kable:OpcUaClient", System.Net.Dns.GetHostName()),
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                { StoreType = "Directory", StorePath = Path.Combine(options.PkiRootPath, "own"), SubjectName = "CN=KableOpcUaClient" },
                TrustedPeerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(options.PkiRootPath, "trusted") },
                TrustedIssuerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(options.PkiRootPath, "issuer") },
                RejectedCertificateStore = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(options.PkiRootPath, "rejected") },
                AutoAcceptUntrustedCertificates = options.AutoAcceptUntrustedCertificates
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = checked((int)options.SessionTimeoutMs) }
        };
        await config.ValidateAsync(ApplicationType.Client).ConfigureAwait(false);
        if (options.AutoAcceptUntrustedCertificates)
            config.CertificateValidator.CertificateValidation += (_, args) =>
                args.Accept = args.Error.StatusCode == StatusCodes.BadCertificateUntrusted;
        return config;
    }

    private async Task<EndpointDescription> SelectSecureEndpointAsync(ApplicationConfiguration config, CancellationToken ct)
    {
#pragma warning disable CS0618
        using var discovery = await DiscoveryClient.CreateAsync(config, new Uri(_endpointUrl),
            EndpointConfiguration.Create(config), DiagnosticsMasks.None, ct).ConfigureAwait(false);
#pragma warning restore CS0618
        var endpoints = await discovery.GetEndpointsAsync(null, ct).ConfigureAwait(false);
        var selected = endpoints.Where(endpoint => endpoint.SecurityMode == _security.SecurityMode &&
            endpoint.SecurityPolicyUri == _security.SecurityPolicyUri &&
            new Uri(endpoint.EndpointUrl).Scheme == new Uri(_endpointUrl).Scheme)
            .OrderByDescending(endpoint => endpoint.SecurityLevel).FirstOrDefault();
        if (selected == null) throw new InvalidOperationException("Server does not offer the required OPC UA security mode and policy.");
        // Use the requested host, not an unauthenticated discovery-provided host, for certificate domain validation.
        var requested = new Uri(_endpointUrl);
        var address = new UriBuilder(selected.EndpointUrl) { Host = requested.Host, Port = requested.Port };
        selected.EndpointUrl = address.Uri.AbsoluteUri;
        return selected;
    }

    private UserIdentity CreateUserIdentity()
    {
        if (_security.UserCertificate != null) return new UserIdentity(new System.Security.Cryptography.X509Certificates.X509Certificate2(_security.UserCertificate));
        if (_security.UserName == null) return new UserIdentity(new AnonymousIdentityToken());
        // The identity retains the password buffer through activation and possible SDK session reconnects.
        _credentialBytes = Encoding.UTF8.GetBytes(_security.Password ?? string.Empty);
        return new UserIdentity(_security.UserName, _credentialBytes);
    }

    private void DisposeIdentity()
    {
        try { _identity?.Dispose(); }
        finally
        {
            _identity = null;
            if (_credentialBytes != null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(_credentialBytes);
            _credentialBytes = null;
        }
    }
}
