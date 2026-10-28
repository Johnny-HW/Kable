namespace Kable.OpcUa.Tests.Subscriptions;

using System.Reflection;
using Xunit;

public sealed class OpcUaShutdownFaultTests
{
    [Fact]
    public async Task Faulted_recovery_still_closes_subscription_session_and_identity()
    {
        await using var server = await LocalOpcUaServer.OpenAsync();
        var bridge = new OpcUaClientBridge(server.Endpoint);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var subscription = await bridge.SubscribeAsync(new[] { server.Node }, ct: timeout.Token);
        var recoveryField = Field("_recovery");
        var running = (Task)recoveryField.GetValue(bridge)!;
        // Inject a fault only at the task boundary; let the real worker finish on cancellation.
        recoveryField.SetValue(bridge, Task.FromException(new IOException("injected recovery fault")));
        var credentials = new byte[] { 1, 2, 3 };
        Field("_credentialBytes").SetValue(bridge, credentials);
        var disposal = bridge.DisposeAsync().AsTask();
        Assert.Same(disposal, bridge.DisposeAsync().AsTask());
        var error = await Assert.ThrowsAnyAsync<Exception>(() => disposal);
        await running.WaitAsync(timeout.Token);
        Assert.Contains("injected recovery fault", error.ToString());
        while (subscription.Values.TryRead(out _)) { }
        Assert.True(subscription.Values.Completion.IsCompleted);
        await subscription.Values.Completion;
        Assert.Null(Field("_session").GetValue(bridge));
        Assert.Null(Field("_identity").GetValue(bridge));
        Assert.Equal(new byte[3], credentials);
        await subscription.DisposeAsync();
    }
    private static FieldInfo Field(string name) => typeof(OpcUaClientBridge).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
