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
public sealed class SessionCorrelationRoutingTests
{
    [Fact]
    public async Task TC_SES_01_RequestAsync_WithCorrelationIdCodec_EnablesOutOfOrderResponses()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new CorrelationIdLineCodec();
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        var taskSlow = session.RequestAsync<string>("CID_1:SLOW_COMMAND", TimeSpan.FromSeconds(5));
        var taskFast = session.RequestAsync<string>("CID_2:FAST_COMMAND", TimeSpan.FromSeconds(5));

        await factory.Context.WriteAsciiLineAsync("CID_2:FAST_RESPONSE", 0x0A);
        var fastRes = await taskFast;
        fastRes.Should().Be("CID_2:FAST_RESPONSE");
        taskSlow.IsCompleted.Should().BeFalse();

        await factory.Context.WriteAsciiLineAsync("CID_1:SLOW_RESPONSE", 0x0A);
        var slowRes = await taskSlow;
        slowRes.Should().Be("CID_1:SLOW_RESPONSE");
    }

    [Fact]
    public async Task TC_SES_02_RequestAsync_LateResponseAfterTimeout_DoesNotPolluteNextRequest()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        Func<Task> actTimeout = async () =>
            await session.RequestAsync<string>("REQ_TIMEOUT", TimeSpan.FromMilliseconds(50));

        await actTimeout.Should().ThrowAsync<DeviceTimeoutException>();

        // Fail-safe industrial design: Session must abort on unkeyed FIFO timeout to prevent late response pollution
        session.IsConnected.Should().BeFalse();

        await factory.Context.WriteAsciiLineAsync("LATE_PHANTOM_RESPONSE", 0x0A);
        await Task.Delay(50);

        // Next request on this disconnected session must fail-fast without data corruption
        Func<Task> actClean = async () => await session.RequestAsync<string>("REQ_CLEAN", TimeSpan.FromSeconds(2));
        await actClean.Should().ThrowAsync<DeviceDisconnectedException>();
    }

    [Fact]
    public async Task TC_SES_04_RequestAsync_InvalidResponseTypeCast_ThrowsInvalidCastAndReleasesLock()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // Expect int, but response is string -> throws InvalidCastException
        var castTask = session.RequestAsync<int>("CMD_NEED_INT", TimeSpan.FromSeconds(3));
        await factory.Context.WriteAsciiLineAsync("NOT_AN_INT", 0x0A);

        Func<Task> act = async () => await castTask;
        await act.Should().ThrowAsync<InvalidCastException>();

        // Verify lock is released and subsequent request succeeds
        var nextTask = session.RequestAsync<string>("CMD_NEXT", TimeSpan.FromSeconds(3));
        await factory.Context.WriteAsciiLineAsync("OK_NEXT", 0x0A);
        var res = await nextTask;
        res.Should().Be("OK_NEXT");
    }

    [Fact]
    public async Task TC_SES_101_Session_200ConcurrentFifoRequests_MaintainsStrictOrderWithoutDeadlock()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        const int requestCount = 100;
        var requestTasks = new Task<string>[requestCount];

        // Server emulation loop: reads each request and writes matching response
        var serverLoop = Task.Run(async () =>
        {
            var reader = factory.Context.RemoteRead;
            int responded = 0;
            while (responded < requestCount)
            {
                var result = await reader.ReadAsync();
                var buffer = result.Buffer;
                while (codec.TryDecode(ref buffer, out var cmd))
                {
                    var resp = "RESP_" + cmd;
                    await factory.Context.WriteAsciiLineAsync(resp, 0x0A);
                    responded++;
                    if (responded >= requestCount) break;
                }
                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) break;
            }
        });

        for (int i = 0; i < requestCount; i++)
        {
            int index = i;
            requestTasks[index] = session.RequestAsync<string>($"FIFO_CMD_{index}", TimeSpan.FromSeconds(10)).AsTask();
        }

        var results = await Task.WhenAll(requestTasks);
        await serverLoop;

        for (int i = 0; i < requestCount; i++)
        {
            results[i].Should().Be($"RESP_FIFO_CMD_{i}");
        }
    }

    [Fact]
    public async Task TC_SES_102_Session_LateResponseAfterTimeout_RoutesToStreamWithoutPollutingNextRequest()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // 1. Request 1 times out quickly (50ms)
        Func<Task> actTimeout = async () =>
            await session.RequestAsync<string>("LATE_REQ_1", TimeSpan.FromMilliseconds(50));
        await actTimeout.Should().ThrowAsync<DeviceTimeoutException>();

        // 2. Late response for Request 1 arrives after session was fail-safe aborted
        session.IsConnected.Should().BeFalse();
        await factory.Context.WriteAsciiLineAsync("LATE_RESP_1", 0x0A);
        await Task.Delay(50);

        // 3. Request 2 on the aborted session must fail-fast without crosstalk
        Func<Task> actReq2 = async () => await session.RequestAsync<string>("NORMAL_REQ_2", TimeSpan.FromSeconds(3));
        await actReq2.Should().ThrowAsync<DeviceDisconnectedException>();
    }

    [Fact]
    public async Task TC_SES_107_Session_CallerCancellationTokenExpired_CancelsWithoutCorruptingEngine()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        using var callerCts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Func<Task> actCanceled = async () =>
            await session.RequestAsync<string>("CANCELED_CMD", TimeSpan.FromSeconds(5), callerCts.Token);
        await actCanceled.Should().ThrowAsync<OperationCanceledException>();

        // Fail-safe industrial design: cancellation after transmit aborts session to prevent crosstalk
        session.IsConnected.Should().BeFalse();

        Func<Task> actNext = async () => await session.RequestAsync<string>("NEXT_HEALTHY_CMD", TimeSpan.FromSeconds(3));
        await actNext.Should().ThrowAsync<DeviceDisconnectedException>();
    }
}
