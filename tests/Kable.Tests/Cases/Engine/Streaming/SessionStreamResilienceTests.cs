namespace Kable.Tests.Cases;

using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Tests.Fixtures;
using Kable.Tests.Fixtures.Codecs;
using Xunit;

[Collection("HardwareTransportTests")]
public sealed class SessionStreamResilienceTests
{
    [Fact]
    public async Task TC_SES_03_RequestAsync_SimultaneousAlarmAndResponse_RoutesCorrectly()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        var reqTask = session.RequestAsync<string>("GET_STATUS", TimeSpan.FromSeconds(5));

        for (int i = 1; i <= 5; i++)
        {
            await factory.Context.WriteAsciiLineAsync($"$ALARM_LEVEL_{i}", 0x0A);
        }

        await factory.Context.WriteAsciiLineAsync("STATUS_NORMAL_OK", 0x0A);

        var response = await reqTask;
        response.Should().Be("STATUS_NORMAL_OK");
    }

    [Fact]
    public async Task TC_SES_06_Stream_ConsumerAbortsMidway_DoesNotBlockSessionReadLoop()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        using var consumerCts = new System.Threading.CancellationTokenSource();

        var consumerTask = Task.Run(async () =>
        {
            await foreach (var item in session.Stream.WithCancellation(consumerCts.Token))
            {
                if (item.StartsWith("$ABORT_CONSUMER"))
                {
                    consumerCts.Cancel();
                }
            }
        });

        await factory.Context.WriteAsciiLineAsync("$START", 0x0A);
        await factory.Context.WriteAsciiLineAsync("$ABORT_CONSUMER", 0x0A);

        Func<Task> actConsumer = async () => await consumerTask;
        await actConsumer.Should().ThrowAsync<OperationCanceledException>();

        // Verify session read loop is still alive and handling requests
        var reqTask = session.RequestAsync<string>("ECHO_AFTER_ABORT", TimeSpan.FromSeconds(3));
        await factory.Context.WriteAsciiLineAsync("ECHO_OK", 0x0A);
        var res = await reqTask;
        res.Should().Be("ECHO_OK");
    }

    [Fact]
    public void TC_SES_108_Session_ObserverQueueOverflow_DropsOldestAndPreservesLatestPacket()
    {
        var observer = new Kable.Observability.CommObserver(bufferCapacity: 3);

        for (int i = 0; i < 10; i++)
        {
            observer.OnPacketTrace(new Kable.Observability.PacketTraceRecord(
                DateTime.UtcNow,
                Kable.Observability.PacketDirection.Rx,
                Kable.Observability.TrafficKind.PeriodicTelemetry,
                "STREAM",
                ReadOnlyMemory<byte>.Empty,
                $"ITEM_{i}",
                TimeSpan.Zero));
        }

        var list = new System.Collections.Generic.List<string>();
        while (observer.PeriodicStream.TryRead(out var rec))
        {
            if (rec.ParsedText != null) list.Add(rec.ParsedText);
        }

        list.Count.Should().Be(3);
        list.Should().ContainInOrder("ITEM_7", "ITEM_8", "ITEM_9");
    }
}
