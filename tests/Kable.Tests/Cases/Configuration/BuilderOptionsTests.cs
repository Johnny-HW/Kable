namespace Kable.Tests.Cases.Configuration;

using System.Buffers;
using System.Text;
using Kable.Codecs;
using Kable.Configuration;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Extensions;
using Kable.Transports;
using Kable.Simple;
using Xunit;

public class BuilderOptionsTests
{
    [Fact]
    public void LegacyConnectTimeout_RemainsEffectiveUntilExplicitlyOverridden()
    {
        var legacy = new KableDeviceOptions { TimeoutMs = 1200 };
        Assert.Equal(1200, legacy.ConnectTimeoutMs);
        Assert.Equal(600, (legacy with { ConnectTimeoutMs = 600 }).ConnectTimeoutMs);
    }

    [Fact]
    public async Task DefaultTimeout_FromDeviceOptions_IsUsedByTypedRequest()
    {
        var (client, server) = InMemoryConnectionContext.CreatePair();
        await using var peer = server;
        await using var session = new KableClientBuilder<string>()
            .UseOptions(new KableDeviceOptions { DefaultRequestTimeoutMs = 40 })
            .UseConnectionFactory(new Factory(client)).UseCodec(new AsciiLineCodec()).Build();
        await session.StartAsync();
        var error = await Assert.ThrowsAsync<DeviceTimeoutException>(() => session.RequestAsync("STATUS").AsTask());
        Assert.Equal(TimeSpan.FromMilliseconds(40), error.Timeout);
    }

    [Fact]
    public async Task ExplicitTimeout_OverridesConfiguredDefault()
    {
        var (client, server) = InMemoryConnectionContext.CreatePair();
        await using var peer = server;
        await using var session = new KableClientBuilder<string>()
            .UseOptions(new KableDeviceOptions { DefaultRequestTimeoutMs = 40 })
            .UseConnectionFactory(new Factory(client)).UseCodec(new AsciiLineCodec()).Build();
        await session.StartAsync();
        var error = await Assert.ThrowsAsync<DeviceTimeoutException>(() =>
            session.RequestAsync("STATUS", TimeSpan.FromMilliseconds(100)).AsTask());
        Assert.Equal(TimeSpan.FromMilliseconds(100), error.Timeout);
    }

    [Fact]
    public void InvalidDefaultTimeout_IsRejectedBeforeConnecting()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KableClientBuilder<string>()
            .UseOptions(new KableDeviceOptions { DefaultRequestTimeoutMs = 0 }));
    }

    [Fact]
    public void Builder_PassesSessionOptions_ToValidation()
    {
        Assert.Throws<InvalidOperationException>(() => new KableClientBuilder<string>()
            .UseTcp("localhost", 9000).UseCodec(new AsciiLineCodec())
            .UseSessionOptions(new KableSessionOptions<string> { AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage })
            .Build());
    }

    [Fact]
    public async Task Builder_Heartbeat_WritesConfiguredPing()
    {
        var (client, server) = InMemoryConnectionContext.CreatePair();
        await using var peer = server;
        await using var session = new KableClientBuilder<string>()
            .UseConnectionFactory(new Factory(client)).UseCodec(new AsciiLineCodec())
            .UseHeartbeat(new HeartbeatOptions<string>(TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(5), () => "PING")).Build();
        await session.StartAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var result = await server.Input.ReadAsync(cts.Token);
        Assert.Equal("PING\n", Encoding.ASCII.GetString(result.Buffer.ToArray()));
        server.Input.AdvanceTo(result.Buffer.End);
    }

    private sealed class Factory(IConnectionContext context) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(context);
    }

    [Fact]
    public async Task SimpleWrapper_PreservesConfiguredSessionTimeout()
    {
        var (connection, server) = InMemoryConnectionContext.CreatePair();
        await using var peer = server;
        var session = new KableClientBuilder<string>()
            .UseOptions(new KableDeviceOptions { DefaultRequestTimeoutMs = 40 })
            .UseConnectionFactory(new Factory(connection)).UseCodec(new AsciiLineCodec()).Build();
        await using var client = await KableSimple.FromSessionAsync(session);
        var error = await Assert.ThrowsAsync<DeviceTimeoutException>(() => client.QueryAsync("STATUS").AsTask());
        Assert.Equal(TimeSpan.FromMilliseconds(40), error.Timeout);
    }
}
