namespace Kable.OpcUa.Tests;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

public sealed class OpcUaHardeningTests
{
    [Fact]
    public void LegacyStringConstructor_IsMarkedObsolete()
    {
        var ctor = typeof(OpcUaClientBridge).GetConstructor(
            new[] { typeof(string), typeof(bool), typeof(uint), typeof(Microsoft.Extensions.Logging.ILogger<OpcUaClientBridge>) });
        Assert.NotNull(ctor);
        Assert.NotNull(ctor!.GetCustomAttribute<ObsoleteAttribute>());
    }

    [Fact]
    public async Task DisposedBridge_AllOperationsThrowObjectDisposedCleanly()
    {
        var bridge = new OpcUaClientBridge(new OpcUaOptions { EndpointUrl = "opc.tcp://127.0.0.1:4840" });
        var first = bridge.DisposeAsync().AsTask();
        var second = bridge.DisposeAsync().AsTask();
        Assert.Same(first, second);
        await first;

        await Assert.ThrowsAsync<ObjectDisposedException>(() => bridge.ConnectAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => bridge.SubscribeAsync(new[] { "ns=2;s=v" }));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => bridge.ReadNodesAsync(new[] { "ns=2;s=v" }));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            bridge.WriteNodesAsync(new[] { new KeyValuePair<string, object>("ns=2;s=v", 1) }));
    }
}
