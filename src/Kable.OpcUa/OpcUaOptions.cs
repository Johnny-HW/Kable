namespace Kable.OpcUa;

using System;

public sealed class OpcUaOptions
{
    public string EndpointUrl { get; set; } = "opc.tcp://localhost:4840";
    public bool AutoAcceptUntrustedCertificates { get; set; } = true;
    public uint SessionTimeoutMs { get; set; } = 60000;
}
