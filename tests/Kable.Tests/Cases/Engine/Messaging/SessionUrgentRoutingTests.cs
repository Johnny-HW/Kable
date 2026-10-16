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
public sealed class SessionUrgentRoutingTests
{
    [Fact]
    public async Task TC_SES_07_SendUrgentAsync_UnderFifoContention_BypassesWaitingQueueImmediately()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // Lock FIFO with slow request
        var slowTask = session.RequestAsync<string>("SLOW_MEASUREMENT", TimeSpan.FromSeconds(5));

        // Send urgent message while slowTask is waiting for response
        await session.SendUrgentAsync("EMERGENCY_SHUTDOWN");

        // Remote reader should receive both commands without deadlock
        var readResult = await factory.Context.RemoteRead.ReadAsync();
        string received = Encoding.UTF8.GetString(System.Buffers.BuffersExtensions.ToArray(readResult.Buffer));
        factory.Context.RemoteRead.AdvanceTo(readResult.Buffer.End);

        received.Should().Contain("SLOW_MEASUREMENT");
        received.Should().Contain("EMERGENCY_SHUTDOWN");

        // Complete the slow request
        await factory.Context.WriteAsciiLineAsync("SLOW_DONE", 0x0A);
        var slowRes = await slowTask;
        slowRes.Should().Be("SLOW_DONE");
    }

    [Fact]
    public async Task TC_SES_103_Session_SendUrgentAsync_BypassesFifoLockInstantly()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // Start a slow FIFO request that holds the fifo lock until response arrives
        var slowFifoTask = session.RequestAsync<string>("SLOW_CMD", TimeSpan.FromSeconds(5)).AsTask();

        // Send urgent message while FIFO lock is busy
        await session.SendUrgentAsync("URGENT_HALT");

        // Read from server side to verify urgent message arrived
        var serverRead = await factory.Context.RemoteRead.ReadAsync();
        string receivedWire = Encoding.UTF8.GetString(System.Buffers.BuffersExtensions.ToArray(serverRead.Buffer));
        factory.Context.RemoteRead.AdvanceTo(serverRead.Buffer.End);

        receivedWire.Should().Contain("URGENT_HALT");

        // Clean up FIFO request
        await factory.Context.WriteAsciiLineAsync("SLOW_ACK", 0x0A);
        await slowFifoTask;
    }
}
