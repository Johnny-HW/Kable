namespace Kable.OpcUa.Tests.Subscriptions;

using Kable.OpcUa.Subscriptions;
using Opc.Ua;
using Xunit;

public sealed class OpcUaSubscriptionTests
{
    [Fact]
    public async Task RealServer_BatchChangeNotificationAndReconnectRestore()
    {
        var server = await LocalOpcUaServer.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var bridge = new OpcUaClientBridge(server.Endpoint);
        try
        {
            await bridge.ConnectAsync(timeout.Token);
            string node = server.Node;
            var read = await bridge.ReadNodesAsync(new[] { node, "ns=2;s=missing" }, timeout.Token);
            Assert.Equal(1, read[0].Value);
            Assert.False(read[1].IsGood);
            await using var subscription = await bridge.SubscribeAsync(new[] { node }, new OpcUaSubscriptionOptions
            { PublishingIntervalMs = 50, SamplingIntervalMs = 25, Capacity = 4 }, timeout.Token);
            var status = await bridge.WriteNodesAsync(new[] { new KeyValuePair<string, object>(node, 42) }, timeout.Token);
            Assert.Equal(StatusCodes.Good, status[0]);
            await ReadUntil(42);
            string endpoint = server.Endpoint;
            await server.DisposeAsync();
            server = await LocalOpcUaServer.OpenAsync(endpoint);
            // KeepAlive detects the dead session and the bridge recreates monitored items.
            await ReadUntil(1);
            await bridge.WriteNodeValueAsync(node, 99, timeout.Token);
            await ReadUntil(99);
            await subscription.DisposeAsync();
            while (subscription.Values.TryRead(out _)) { }
            await subscription.Values.Completion.WaitAsync(timeout.Token);

            async Task ReadUntil(int expected)
            {
                while (true)
                {
                    var update = await subscription.Values.ReadAsync(timeout.Token);
                    if (update.Value is int value && value == expected) return;
                }
            }
        }
        finally { await server.DisposeAsync(); }
    }
}
