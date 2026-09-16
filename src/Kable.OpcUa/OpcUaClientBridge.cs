namespace Kable.OpcUa;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.OpcUa.Models;
using Opc.Ua;
using Opc.Ua.Client;

/// <summary>
/// 공식 OPCFoundation 스택 기반의 경량 OPC UA 클라이언트 브리지.
/// </summary>
public sealed class OpcUaClientBridge : IAsyncDisposable
{
    private readonly string _endpointUrl;
    private readonly bool _autoAcceptUntrustedCertificates;
    private Session? _session;
    private ApplicationConfiguration? _configuration;
    private int _isDisposed;

    public bool IsConnected => _session != null && _session.Connected;
    public string EndpointUrl => _endpointUrl;

    public OpcUaClientBridge(string endpointUrl, bool autoAcceptUntrustedCertificates = true)
    {
        _endpointUrl = endpointUrl ?? throw new ArgumentNullException(nameof(endpointUrl));
        _autoAcceptUntrustedCertificates = autoAcceptUntrustedCertificates;
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
        if (IsConnected) return;

        _configuration ??= await CreateDefaultConfigurationAsync(_autoAcceptUntrustedCertificates).ConfigureAwait(false);

        var endpoint = CoreClientUtils.SelectEndpoint(_configuration, _endpointUrl, useSecurity: false);
        var endpointConfiguration = EndpointConfiguration.Create(_configuration);
        var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfiguration);

        _session = await Session.Create(
            configuration: _configuration,
            endpoint: configuredEndpoint,
            updateBeforeConnect: false,
            sessionName: "KableClientSession",
            sessionTimeout: 60000,
            identity: new UserIdentity(new AnonymousIdentityToken()),
            preferredLocales: null).ConfigureAwait(false);
    }

    /// <summary>
    /// 특정 NodeId의 값을 비동기로 읽어옵니다.
    /// </summary>
    public async Task<OpcUaDataValue> ReadNodeValueAsync(string nodeIdString, CancellationToken ct = default)
    {
        if (_session == null || !_session.Connected)
        {
            throw new InvalidOperationException("OPC UA session is not connected. Call ConnectAsync() first.");
        }

        var nodeId = new NodeId(nodeIdString);
        var dataValue = await _session.ReadValueAsync(nodeId, ct).ConfigureAwait(false);

        return new OpcUaDataValue(
            nodeId: nodeIdString,
            value: dataValue.Value,
            statusCode: dataValue.StatusCode.Code,
            sourceTimestamp: dataValue.SourceTimestamp);
    }

    /// <summary>
    /// 특정 NodeId에 값을 비동기로 기록합니다.
    /// </summary>
    public async Task WriteNodeValueAsync(string nodeIdString, object value, CancellationToken ct = default)
    {
        if (_session == null || !_session.Connected)
        {
            throw new InvalidOperationException("OPC UA session is not connected. Call ConnectAsync() first.");
        }

        var nodeId = new NodeId(nodeIdString);
        var writeValue = new WriteValue
        {
            NodeId = nodeId,
            AttributeId = Attributes.Value,
            Value = new DataValue(new Variant(value))
        };

        var writeCollection = new WriteValueCollection { writeValue };

        var response = await _session.WriteAsync(
            null,
            writeCollection,
            ct).ConfigureAwait(false);

        var results = response.Results;
        if (results.Count > 0 && StatusCode.IsBad(results[0]))
        {
            throw new InvalidOperationException($"OPC UA Write Failed for Node '{nodeIdString}': StatusCode 0x{results[0].Code:X8}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

        if (_session != null)
        {
            try
            {
                await _session.CloseAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore disconnect error on shutdown
            }
            finally
            {
                _session.Dispose();
            }
        }
    }
}
