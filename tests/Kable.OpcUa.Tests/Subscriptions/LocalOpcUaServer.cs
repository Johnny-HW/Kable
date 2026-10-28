namespace Kable.OpcUa.Tests.Subscriptions;

using System.Net;
using System.Net.Sockets;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

#pragma warning disable CS0618
internal sealed class LocalOpcUaServer : IAsyncDisposable
{
    private readonly ApplicationInstance _application;
    private readonly TestServer _server = new();
    public string Endpoint { get; }
    public string Node => _server.Manager!.ValueNode.NodeId.ToString();
    private LocalOpcUaServer(string endpoint, ApplicationInstance application) { Endpoint = endpoint; _application = application; }
    internal static async Task<LocalOpcUaServer> OpenAsync(string? endpoint = null)
    {
        if (endpoint == null)
        {
            using var picker = new TcpListener(IPAddress.Loopback, 0); picker.Start();
            endpoint = $"opc.tcp://localhost:{((IPEndPoint)picker.LocalEndpoint).Port}/KableTest";
        }
        string root = Path.Combine(Path.GetTempPath(), "Kable-OpcUa-Tests", Guid.NewGuid().ToString("N"));
        var config = new ApplicationConfiguration
        {
            ApplicationName = "KableTestServer", ApplicationUri = "urn:localhost:KableTestServer", ApplicationType = ApplicationType.Server,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier { StoreType = "Directory", StorePath = Path.Combine(root, "own"), SubjectName = "CN=KableTestServer" },
                TrustedPeerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(root, "trusted") },
                TrustedIssuerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(root, "issuer") },
                RejectedCertificateStore = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(root, "rejected") },
                AutoAcceptUntrustedCertificates = true
            },
            TransportQuotas = new TransportQuotas { OperationTimeout = 2000 },
            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = new StringCollection { endpoint },
                SecurityPolicies = new ServerSecurityPolicyCollection { new ServerSecurityPolicy { SecurityMode = MessageSecurityMode.None, SecurityPolicyUri = SecurityPolicies.None } },
                UserTokenPolicies = new UserTokenPolicyCollection { new UserTokenPolicy(UserTokenType.Anonymous) },
                DiagnosticsEnabled = false, MinSessionTimeout = 1000, MaxSessionTimeout = 60000
            }
        };
        await config.ValidateAsync(ApplicationType.Server);
        var application = new ApplicationInstance { ApplicationName = config.ApplicationName, ApplicationType = ApplicationType.Server, ApplicationConfiguration = config };
        await application.CheckApplicationInstanceCertificatesAsync(true);
        var result = new LocalOpcUaServer(endpoint, application);
        await application.StartAsync(result._server);
        return result;
    }
    public async ValueTask DisposeAsync() { await _application.StopAsync(); _server.Dispose(); }

    private sealed class TestServer : StandardServer
    {
        internal TestNodeManager? Manager;
        protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            Manager = new TestNodeManager(server, configuration);
            return new MasterNodeManager(server, configuration, null, Manager);
        }
        protected override ServerProperties LoadServerProperties() => new()
        { ManufacturerName = "Kable", ProductName = "LocalTest", ProductUri = "urn:Kable:Test", SoftwareVersion = "1.0", BuildNumber = "1", BuildDate = DateTime.UtcNow };
    }
    private sealed class TestNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        : CustomNodeManager2(server, configuration, "urn:Kable:Tests")
    {
        internal BaseDataVariableState ValueNode = null!;
        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                ValueNode = new BaseDataVariableState(null)
                {
                    NodeId = new NodeId("Value", NamespaceIndexes[0]), BrowseName = new QualifiedName("Value", NamespaceIndexes[0]),
                    DisplayName = "Value", TypeDefinitionId = VariableTypeIds.BaseDataVariableType, DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar, AccessLevel = AccessLevels.CurrentReadOrWrite, UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 1, StatusCode = StatusCodes.Good, Timestamp = DateTime.UtcNow
                };
                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var references))
                    externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, ValueNode.NodeId));
                ValueNode.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
                AddPredefinedNode(SystemContext, ValueNode);
            }
        }
    }
}
#pragma warning restore CS0618
