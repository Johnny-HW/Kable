namespace Kable.OpcUa;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kable.OpcUa.Models;
using Opc.Ua;
using Opc.Ua.Client;

/// <summary>
/// 공식 OPCFoundation 스택 기반의 경량 OPC UA 클라이언트 브리지.
/// </summary>
public sealed partial class OpcUaClientBridge : IOpcUaClientBridge
{
    private readonly string _endpointUrl;
    private readonly bool _autoAcceptUntrustedCertificates;
    private readonly uint _sessionTimeoutMs;
    private readonly ILogger<OpcUaClientBridge>? _logger;
    private Session? _session;
    private ApplicationConfiguration? _configuration;
    private int _isDisposed;

    public bool IsConnected => Volatile.Read(ref _isDisposed) == 0 && Volatile.Read(ref _needsReconnect) == 0 && _session != null && _session.Connected;
    public string EndpointUrl => _endpointUrl;

    public OpcUaClientBridge(
        string endpointUrl,
        bool autoAcceptUntrustedCertificates = true,
        uint sessionTimeoutMs = 60000,
        ILogger<OpcUaClientBridge>? logger = null)
    {
        _endpointUrl = endpointUrl ?? throw new ArgumentNullException(nameof(endpointUrl));
        _autoAcceptUntrustedCertificates = autoAcceptUntrustedCertificates;
        _sessionTimeoutMs = sessionTimeoutMs;
        _logger = logger;
    }

    public OpcUaClientBridge(
        IOptions<OpcUaOptions> options,
        ILogger<OpcUaClientBridge>? logger = null)
    {
        var opt = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _endpointUrl = opt.EndpointUrl;
        _autoAcceptUntrustedCertificates = opt.AutoAcceptUntrustedCertificates;
        _sessionTimeoutMs = opt.SessionTimeoutMs;
        _logger = logger;
    }

    private static async Task<ApplicationConfiguration> CreateDefaultConfigurationAsync(bool autoAcceptCertificates)
    {
        var config = new ApplicationConfiguration
        {
            ApplicationName = "Kable.OpcUa.Client",
            ApplicationUri = Utils.Format(@"urn:{0}:Kable:OpcUaClient", System.Net.Dns.GetHostName()),
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%LocalApplicationData%/Kable/pki/own", SubjectName = "KableOpcUaClient" },
                TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%LocalApplicationData%/Kable/pki/issuer" },
                TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%LocalApplicationData%/Kable/pki/trusted" },
                RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%LocalApplicationData%/Kable/pki/rejected" },
                AutoAcceptUntrustedCertificates = autoAcceptCertificates
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        };

        await config.ValidateAsync(ApplicationType.Client).ConfigureAwait(false);

        if (autoAcceptCertificates)
        {
            config.CertificateValidator.CertificateValidation += (validator, e) =>
            {
                e.Accept = true;
            };
        }

        return config;
    }

    /// <summary>
    /// OPC UA 서버와 세션을 수립합니다.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
        await _sessionGate.WaitAsync(linked.Token).ConfigureAwait(false);
        try { await ConnectAndRestoreAsync(linked.Token).ConfigureAwait(false); }
        finally { _sessionGate.Release(); }
    }

    /// <summary>
    /// 특정 NodeId의 값을 비동기로 읽어옵니다.
    /// </summary>
    public async Task<OpcUaDataValue> ReadNodeValueAsync(string nodeIdString, CancellationToken ct = default) =>
        (await ReadNodesAsync(new[] { nodeIdString }, ct).ConfigureAwait(false))[0];

    /// <summary>
    /// 특정 NodeId에 값을 비동기로 기록합니다.
    /// </summary>
    public async Task WriteNodeValueAsync(string nodeIdString, object value, CancellationToken ct = default)
    {
        var status = (await WriteNodesAsync(new[] { new System.Collections.Generic.KeyValuePair<string, object>(nodeIdString, value) }, ct).ConfigureAwait(false))[0];
        if (StatusCode.IsBad(status))
            throw new InvalidOperationException($"OPC UA Write Failed for Node '{nodeIdString}': StatusCode 0x{status:X8}");
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetime) return new ValueTask(_disposal ??= DisposeCoreAsync());
    }
}
