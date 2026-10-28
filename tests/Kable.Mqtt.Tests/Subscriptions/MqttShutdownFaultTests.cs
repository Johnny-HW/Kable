namespace Kable.Mqtt.Tests.Subscriptions;

using System.Reflection;
using Kable.Mqtt.Subscriptions;
using MQTTnet.Client;
using MQTTnet.Packets;
using Xunit;

public sealed class MqttShutdownFaultTests
{
    [Fact]
    public async Task Faulted_recovery_still_completes_message_channel_and_shares_disposal_failure()
    {
        var subscriber = new MqttSubscriber(new MqttClientOptionsBuilder().WithTcpServer("localhost").Build(), Array.Empty<MqttTopicFilter>());
        var fault = new IOException("injected recovery fault");
        // Inject an SDK worker failure at its task boundary without adding a production test API.
        typeof(MqttSubscriber).GetField("_worker", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(subscriber, Task.FromException(fault));
        var first = subscriber.DisposeAsync().AsTask();
        var second = subscriber.DisposeAsync().AsTask();
        Assert.Same(first, second);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => first);
        Assert.Contains("injected recovery fault", error.ToString());
        Assert.True(subscriber.Messages.Completion.IsCompleted);
        await subscriber.Messages.Completion;
    }
}
