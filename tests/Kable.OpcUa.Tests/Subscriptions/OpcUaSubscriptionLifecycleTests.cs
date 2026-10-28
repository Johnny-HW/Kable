namespace Kable.OpcUa.Tests.Subscriptions;

using Kable.OpcUa.Subscriptions;
using Opc.Ua;
using Xunit;

public sealed class OpcUaSubscriptionLifecycleTests
{
    [Fact]
    public async Task InvalidNodesCancellationAndBoundedQueue()
    {
        await using var server = await LocalOpcUaServer.OpenAsync();
        await using var bridge = new OpcUaClientBridge(server.Endpoint);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await bridge.ConnectAsync(timeout.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.SubscribeAsync(new[] { "ns=2;s=missing" }, ct: timeout.Token));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bridge.ReadNodesAsync(new[] { server.Node }, canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bridge.SubscribeAsync(new[] { server.Node }, ct: canceled.Token));
        await using var subscription = await bridge.SubscribeAsync(new[] { server.Node }, new OpcUaSubscriptionOptions
        { Capacity = 1, SamplingIntervalMs = 10, PublishingIntervalMs = 20 }, timeout.Token);
        while (!subscription.Values.TryPeek(out _)) await Task.Delay(20, timeout.Token);
        for (int i = 2; i <= 8; i++)
        {
            await bridge.WriteNodeValueAsync(server.Node, i, timeout.Token);
            await Task.Delay(100, timeout.Token);
        }
        while (subscription.DroppedValues == 0) await Task.Delay(20, timeout.Token);
        Assert.Equal(1, subscription.Values.Count);
        var statuses = await bridge.WriteNodesAsync(new[]
        {
            new KeyValuePair<string, object>(server.Node, 100),
            new KeyValuePair<string, object>("ns=2;s=missing", 100)
        }, timeout.Token);
        Assert.Equal(StatusCodes.Good, statuses[0]);
        Assert.True(StatusCode.IsBad(statuses[1]));
        await bridge.DisposeAsync();
        while (subscription.Values.TryRead(out _)) { }
        await subscription.Values.Completion.WaitAsync(timeout.Token);
        Assert.False(bridge.IsConnected);
    }
}
