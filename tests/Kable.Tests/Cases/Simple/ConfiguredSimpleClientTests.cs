namespace Kable.Tests.Cases.Simple;

using System.Net;
using Kable.Configuration;
using Kable.Exceptions;
using Kable.Simple;
using Kable.Transports;
using Xunit;

[Collection("HardwareTransportTests")]
public class ConfiguredSimpleClientTests
{
    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 100)]
    public async Task OpenDeviceOptions_UsesConfiguredDefault_WithSimpleOverride(bool useOverride, int expectedMs)
    {
        await using var listener = new TcpConnectionListener(IPAddress.Loopback, 0);
        int port = ((IPEndPoint)listener.LocalEndPoint).Port;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var accept = listener.AcceptAsync(deadline.Token);
        var device = new KableDeviceOptions
        { Host = "127.0.0.1", Port = port, DefaultRequestTimeoutMs = 40 };
        var simple = useOverride ? new KableSimpleOptions { DefaultTimeout = TimeSpan.FromMilliseconds(100) } : null;
        await using var client = await KableSimple.OpenAsync(device, simple, deadline.Token);
        await using var server = await accept;
        var error = await Assert.ThrowsAsync<DeviceTimeoutException>(() => client.QueryAsync("STATUS").AsTask());
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), error.Timeout);
        Assert.False(client.IsConnected);
    }
}
