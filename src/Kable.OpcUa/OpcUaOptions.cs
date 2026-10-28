namespace Kable.OpcUa;

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Opc.Ua;

public sealed class OpcUaOptions
{
    public string EndpointUrl { get; set; } = "opc.tcp://localhost:4840";
    public bool AutoAcceptUntrustedCertificates { get; set; }
    public uint SessionTimeoutMs { get; set; } = 60000;
    public MessageSecurityMode SecurityMode { get; set; } = MessageSecurityMode.SignAndEncrypt;
    public string SecurityPolicyUri { get; set; } = SecurityPolicies.Basic256Sha256;
    public string PkiRootPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kable", "pki");
    public string? UserName { get; set; }
    public string? Password { get; set; }
    /// <summary>Certificate identity with a private key. The caller retains ownership.</summary>
    public X509Certificate2? UserCertificate { get; set; }

    internal OpcUaOptions Snapshot()
    {
        if (!Uri.TryCreate(EndpointUrl, UriKind.Absolute, out _)) throw new ArgumentException("An absolute OPC UA endpoint is required.");
        if (SessionTimeoutMs == 0 || SessionTimeoutMs > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(SessionTimeoutMs));
        if (SecurityMode is not (MessageSecurityMode.None or MessageSecurityMode.Sign or MessageSecurityMode.SignAndEncrypt))
            throw new ArgumentOutOfRangeException(nameof(SecurityMode));
        if (string.IsNullOrWhiteSpace(SecurityPolicyUri) || (SecurityMode == MessageSecurityMode.None) != (SecurityPolicyUri == SecurityPolicies.None))
            throw new ArgumentException("Security mode and policy must agree.");
        if (UserName != null && UserCertificate != null) throw new ArgumentException("Choose one user identity.");
        if (UserName != null && SecurityMode != MessageSecurityMode.SignAndEncrypt)
            throw new ArgumentException("Username credentials require SignAndEncrypt.");
        if (Password != null && UserName == null) throw new ArgumentException("Password requires a username.");
        if (UserCertificate != null && !UserCertificate.HasPrivateKey) throw new ArgumentException("User certificate requires its private key.");
        return new OpcUaOptions
        {
            EndpointUrl = EndpointUrl, AutoAcceptUntrustedCertificates = AutoAcceptUntrustedCertificates,
            SessionTimeoutMs = SessionTimeoutMs, SecurityMode = SecurityMode, SecurityPolicyUri = SecurityPolicyUri,
            PkiRootPath = Path.GetFullPath(PkiRootPath), UserName = UserName, Password = Password, UserCertificate = UserCertificate
        };
    }
}
