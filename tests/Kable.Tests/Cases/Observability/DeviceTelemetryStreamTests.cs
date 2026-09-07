namespace Kable.Tests.Cases.Observability;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Observability;
using Xunit;

public class DeviceTelemetryStreamTests
{
    [Fact]
    public async Task PublishAndSubscribe_TransfersDataCleanly()
    {
        using var stream = new DeviceTelemetryStream<int>(bufferCapacity: 10);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var consumerTask = Task.Run(async () =>
        {
            var received = new System.Collections.Generic.List<int>();
            await foreach (var item in stream.Subscribe(cts.Token))
            {
                received.Add(item);
                if (received.Count == 3) break;
            }
            return received;
        });

        // 잠시 대기하여 구독 채널 등록 보장
        await Task.Delay(50);
        Assert.Equal(1, stream.SubscriberCount);

        stream.Publish(10);
        stream.Publish(20);
        stream.Publish(30);

        var result = await consumerTask;
        Assert.Equal([10, 20, 30], result);

        // 컨슈머 루프 종료 후 구독자 자동 정리 검증
        await Task.Delay(50);
        Assert.Equal(0, stream.SubscriberCount);
    }

    [Fact]
    public void PublishWithoutSubscribers_IsNonBlockingAndNoOp()
    {
        using var stream = new DeviceTelemetryStream<string>();
        stream.Publish("LostInEther");
        Assert.Equal(0, stream.SubscriberCount);
    }
}
