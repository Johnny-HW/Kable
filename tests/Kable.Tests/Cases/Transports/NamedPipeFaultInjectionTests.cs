namespace Kable.Tests.Cases;

using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Extensions;
using Kable.Transports;
using Xunit;

[Collection("HardwareTransportTests")]
public sealed class NamedPipeFaultInjectionTests
{
    private static async Task WaitUntilDisconnectedAsync(IDeviceSession<string> session, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();
        while (session.IsConnected && deadline.Elapsed < timeout)
        {
            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task TC_TRN_01_NamedPipe_ServerCrash_TriggersConnectionClosedInstantly()
    {
        string pipeName = "fault_pipe_" + Guid.NewGuid().ToString("N");

        var serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        var serverAcceptTask = serverPipe.WaitForConnectionAsync();

        try
        {
            await using var session = new KableClientBuilder<string>()
                .UseNamedPipe(pipeName, timeoutMs: 3000)
                .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
                .Build();

            await session.StartAsync();
            await serverAcceptTask;

            session.IsConnected.Should().BeTrue();

            serverPipe.Dispose();

            await WaitUntilDisconnectedAsync(session, TimeSpan.FromSeconds(3));
            var pendingReq = session.RequestAsync<string>("PING_CRASH", TimeSpan.FromSeconds(3)).AsTask();

            Func<Task> act = async () => await pendingReq;
            await act.Should().ThrowAsync<DeviceDisconnectedException>();

            session.IsConnected.Should().BeFalse();
        }
        finally
        {
            serverPipe.Dispose();
        }
    }

    [Fact]
    public async Task TC_TRN_02_NamedPipe_ConnectionTimeout_ThrowsCleanTimeoutException()
    {
        string nonExistentPipe = "non_existent_pipe_" + Guid.NewGuid().ToString("N");
        var factory = new NamedPipeConnectionFactory(nonExistentPipe, timeoutMs: 100);

        Func<Task> act = async () => await factory.ConnectAsync();
        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task TC_TRN_102_NamedPipe_ServerProcessAbruptTermination_DetectsPipeBroken()
    {
        string pipeName = "test_broken_pipe_" + Guid.NewGuid().ToString("N");
        var serverPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        var serverWait = serverPipe.WaitForConnectionAsync();

        try
        {
            await using var session = new KableClientBuilder<string>()
                .UseNamedPipe(pipeName, timeoutMs: 2000)
                .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
                .Build();

            await session.StartAsync();
            await serverWait;

            session.IsConnected.Should().BeTrue();

            serverPipe.Dispose();

            await WaitUntilDisconnectedAsync(session, TimeSpan.FromSeconds(3));
            var pendingReq = session.RequestAsync<string>("SHOULD_FAIL", TimeSpan.FromSeconds(3)).AsTask();
            Func<Task> act = async () => await pendingReq;
            await act.Should().ThrowAsync<DeviceDisconnectedException>();

            session.IsConnected.Should().BeFalse();
        }
        finally
        {
            serverPipe.Dispose();
        }
    }

    [Fact]
    public async Task TC_TRN_108_NamedPipe_NonExistentServer_ConnectAsyncTimesOutCleanly()
    {
        string nonExistentPipe = "non_existent_pipe_" + Guid.NewGuid().ToString("N");
        var factory = new NamedPipeConnectionFactory(nonExistentPipe, timeoutMs: 300);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Func<Task> act = async () => await factory.ConnectAsync(cts.Token);

        await act.Should().ThrowAsync<TimeoutException>();
    }
}
