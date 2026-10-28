namespace Kable.OpcUa.Tests.Security;

using System.Security.Cryptography.X509Certificates;
using Kable.OpcUa.Tests.Subscriptions;
using Opc.Ua;
using Xunit;

public sealed class OpcUaSecurityTests
{
    [Fact]
    public void OptionsHaveSecureDefaultsAndRejectCredentialsOnUnsecuredChannel()
    {
        var options = new OpcUaOptions();
        Assert.Equal(MessageSecurityMode.SignAndEncrypt, options.SecurityMode);
        Assert.False(options.AutoAcceptUntrustedCertificates);
        Assert.Throws<ArgumentException>(() => new OpcUaClientBridge(new OpcUaOptions
        { SecurityMode = MessageSecurityMode.None, SecurityPolicyUri = SecurityPolicies.None, UserName = "operator", Password = "test-only-password" }));
    }

    [Fact]
    public async Task SecureOptionsNeverDowngradeToUnsecuredServer()
    {
        await using var server = await LocalOpcUaServer.OpenAsync();
        await using var bridge = new OpcUaClientBridge(Options(server));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.ConnectAsync(timeout.Token));
        Assert.False(bridge.IsConnected);
    }

    [Fact]
    public async Task UntrustedServerFailsAndExplicitTrustAllowsEncryptedSession()
    {
        await using var server = await LocalOpcUaServer.OpenAsync(secure: true);
        var options = Options(server);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await using (var rejected = new OpcUaClientBridge(options))
        {
            var error = await Assert.ThrowsAsync<ServiceResultException>(() => rejected.ConnectAsync(timeout.Token));
            Assert.NotEqual(StatusCodes.Good, error.StatusCode);
            Assert.False(rejected.IsConnected);
        }
        Trust(options, server.Certificate);
        await using var accepted = new OpcUaClientBridge(options);
        await accepted.ConnectAsync(timeout.Token);
        Assert.Equal(1, (await accepted.ReadNodeValueAsync(server.Node, timeout.Token)).Value);
    }

    [Theory]
    [InlineData("host")]
    [InlineData("expired")]
    public async Task DevelopmentTrustOptInDoesNotAcceptOtherCertificateErrors(string errorKind)
    {
        await using var server = await LocalOpcUaServer.OpenAsync(secure: true, certificateError: errorKind);
        var options = Options(server); options.AutoAcceptUntrustedCertificates = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var bridge = new OpcUaClientBridge(options);
        await Assert.ThrowsAsync<ServiceResultException>(() => bridge.ConnectAsync(timeout.Token));
        Assert.False(bridge.IsConnected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SecureUserAuthenticationUsesConfiguredIdentity(bool certificateIdentity)
    {
        await using var server = await LocalOpcUaServer.OpenAsync(secure: true, requireUser: true);
        var options = Options(server); Trust(options, server.Certificate);
        if (certificateIdentity) options.UserCertificate = server.Certificate;
        else { options.UserName = "operator"; options.Password = "test-only-password"; }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var bridge = new OpcUaClientBridge(options);
        await bridge.ConnectAsync(timeout.Token);
        Assert.True(bridge.IsConnected);
        if (!certificateIdentity)
        {
            options.Password = "wrong-test-password";
            await using var invalid = new OpcUaClientBridge(options);
            await Assert.ThrowsAsync<ServiceResultException>(() => invalid.ConnectAsync(timeout.Token));
        }
    }

    private static OpcUaOptions Options(LocalOpcUaServer server) => new()
    { EndpointUrl = server.Endpoint, PkiRootPath = Path.Combine(Path.GetTempPath(), "Kable-OpcUa-Clients", Guid.NewGuid().ToString("N")) };
    private static void Trust(OpcUaOptions options, X509Certificate2 certificate)
    {
        string directory = Path.Combine(options.PkiRootPath, "trusted", "certs");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, certificate.Thumbprint + ".der"), certificate.Export(X509ContentType.Cert));
    }
}
