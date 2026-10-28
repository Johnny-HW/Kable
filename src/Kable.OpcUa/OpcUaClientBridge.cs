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
    private readonly OpcUaOptions _security;
    private readonly uint _sessionTimeoutMs;
    private readonly ILogger<OpcUaClientBridge>? _logger;
    private Session? _session;
    private ApplicationConfiguration? _configuration;
    private int _isDisposed;

    public bool IsConnected => Volatile.Read(ref _isDisposed) == 0 && Volatile.Read(ref _needsReconnect) == 0 && _session != null && _session.Connected;
    public string EndpointUrl => _endpointUrl;

    /// <summary>Legacy anonymous/unsecured path. Prefer the Options constructor for secure defaults.</summary>
    public OpcUaClientBridge(string endpointUrl, bool autoAcceptUntrustedCertificates = true,
        uint sessionTimeoutMs = 60000, ILogger<OpcUaClientBridge>? logger = null)
        : this(new OpcUaOptions
        {
            EndpointUrl = endpointUrl, AutoAcceptUntrustedCertificates = autoAcceptUntrustedCertificates,
            SessionTimeoutMs = sessionTimeoutMs, SecurityMode = MessageSecurityMode.None, SecurityPolicyUri = SecurityPolicies.None
        }, logger) { }

    public OpcUaClientBridge(IOptions<OpcUaOptions> options, ILogger<OpcUaClientBridge>? logger = null)
        : this((options ?? throw new ArgumentNullException(nameof(options))).Value, logger) { }

    public OpcUaClientBridge(OpcUaOptions options, ILogger<OpcUaClientBridge>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _security = options.Snapshot();
        _endpointUrl = _security.EndpointUrl;
        _sessionTimeoutMs = _security.SessionTimeoutMs;
        _logger = logger;
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
