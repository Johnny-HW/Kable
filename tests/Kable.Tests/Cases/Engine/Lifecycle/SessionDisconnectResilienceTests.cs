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
public sealed class SessionDisconnectResilienceTests
{
    [Fact]
    public async Task TC_SES_08_OnConnectionClosed_MultipleConcurrentCallers_AllReceiveFailFastException()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new CorrelationIdLineCodec();
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        const int callerCount = 20;
        var tasks = new Task[callerCount];
        for (int i = 0; i < callerCount; i++)
        {
            int id = i;
            tasks[i] = session.RequestAsync<string>($"CID_{id}:HOLD", TimeSpan.FromSeconds(10)).AsTask();
        }

        await Task.Delay(50);

        factory.Context.Abort("Physical Wire Cut");

        for (int i = 0; i < callerCount; i++)
        {
            Func<Task> act = async () => await tasks[i];
            await act.Should().ThrowAsync<DeviceDisconnectedException>();
        }

        session.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task TC_SES_05_StartAsync_MultipleCalls_IdempotentAndThreadSafe()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);

        // Multiple concurrent starts
        var t1 = session.StartAsync().AsTask();
        var t2 = session.StartAsync().AsTask();
        var t3 = session.StartAsync().AsTask();

        await Task.WhenAll(t1, t2, t3);

        session.IsConnected.Should().BeTrue();

        var pingTask = session.RequestAsync<string>("PING_IDEMPOTENT", TimeSpan.FromSeconds(3));
        await factory.Context.WriteAsciiLineAsync("PONG", 0x0A);
        var res = await pingTask;
        res.Should().Be("PONG");
    }

    [Fact]
    public async Task TC_SES_104_Session_AbruptDisconnect_Cancels100PendingRequestsFailFast()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new CorrelationIdLineCodec();
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        const int pendingCount = 50;
        var tasks = new Task[pendingCount];
        for (int i = 0; i < pendingCount; i++)
        {
            tasks[i] = session.RequestAsync<string>($"CID_{i}:CMD", TimeSpan.FromSeconds(10)).AsTask();
        }

        // Allow all 50 caller tasks to enqueue and enter pending wait
        await Task.Delay(50);

        // Abrupt physical disconnect
        factory.Context.Abort("Physical disconnect triggered");

        // All 50 pending requests must throw DeviceDisconnectedException immediately without waiting for 10s timeout
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < pendingCount; i++)
        {
            Func<Task> act = async () => await tasks[i];
            await act.Should().ThrowAsync<DeviceDisconnectedException>();
        }
        sw.Stop();
        sw.ElapsedMilliseconds.Should().BeLessThan(3000);
        session.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task TC_SES_105_Session_MultipleStartAndStop_MaintainsIdempotency()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);

        // 10 concurrent starts
        var startTasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            startTasks[i] = session.StartAsync().AsTask();
        }
        await Task.WhenAll(startTasks);
        session.IsConnected.Should().BeTrue();

        // 5 concurrent stops
        var stopTasks = new Task[5];
        for (int i = 0; i < 5; i++)
        {
            stopTasks[i] = session.StopAsync().AsTask();
        }
        await Task.WhenAll(stopTasks);
        session.IsConnected.Should().BeFalse();
    }
}
