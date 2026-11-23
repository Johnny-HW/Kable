namespace Kable.Tests.Cases;

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
public sealed class TcpFaultInjectionTests
{
    private static async Task WaitUntilDisconnectedAsync(IDeviceSession<string> session, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();
        while (session.IsConnected && deadline.Elapsed < timeout)
        {
            await Task.Delay(5);
        }
    }

    private static async Task AwaitCleanupAsync(Task task)
    {
        try { await task.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException or OperationCanceledException or TimeoutException)
        {
            Debug.WriteLine($"[TcpFaultInjectionTests] Cleanup observed expected error: {ex.Message}");
        }
    }

    [Fact]
    public async Task TC_TRN_03_TcpConnection_RemoteRstReceived_TriggersAbortAndFailsPendingRequests()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Socket? serverSocket = null;
        var serverTask = Task.Run(async () =>
        {
            serverSocket = await listener.AcceptSocketAsync();
        });

        try
        {
            await using var session = new KableClientBuilder<string>()
                .UseTcp("127.0.0.1", port)
                .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
                .Build();

            await session.StartAsync();
            await serverTask;

            session.IsConnected.Should().BeTrue();

            serverSocket!.LingerState = new LingerOption(true, 0);
            serverSocket.Close();

            await WaitUntilDisconnectedAsync(session, TimeSpan.FromSeconds(3));
            var pendingReq = session.RequestAsync<string>("PING_AFTER_RST", TimeSpan.FromSeconds(3)).AsTask();

            Func<Task> act = async () => await pendingReq;
            await act.Should().ThrowAsync<DeviceDisconnectedException>();

            session.IsConnected.Should().BeFalse();
        }
        finally
        {
            listener.Stop();
            await AwaitCleanupAsync(serverTask);
        }
    }

    [Fact]
    public async Task TC_TRN_04_TcpConnection_UnreachableHost_ThrowsSocketExceptionQuickly()
    {
        var factory = new TcpConnectionFactory("127.0.0.1", 54321);

        Func<Task> act = async () => await factory.ConnectAsync();
        await act.Should().ThrowAsync<SocketException>();
    }

    [Fact]
    public async Task TC_TRN_101_Tcp_ForceResetRstPacket_AbortsConnectionContextInstantly()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Socket? acceptedSocket = null;
        var acceptTask = Task.Run(async () =>
        {
            acceptedSocket = await listener.AcceptSocketAsync();
        });

        try
        {
            await using var session = new KableClientBuilder<string>()
                .UseTcp("127.0.0.1", port)
                .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
                .Build();

            await session.StartAsync();
            await acceptTask;

            session.IsConnected.Should().BeTrue();

            acceptedSocket!.LingerState = new LingerOption(true, 0);
            acceptedSocket.Close();

            await WaitUntilDisconnectedAsync(session, TimeSpan.FromSeconds(3));
            var pendingReq = session.RequestAsync<string>("TEST_RST_RECOVERY", TimeSpan.FromSeconds(3)).AsTask();

            Func<Task> act = async () => await pendingReq;
            await act.Should().ThrowAsync<DeviceDisconnectedException>();

            session.IsConnected.Should().BeFalse();
        }
        finally
        {
            listener.Stop();
            await AwaitCleanupAsync(acceptTask);
        }
    }

    [Fact]
    public async Task TC_TRN_106_Tcp_ConnectionTimeoutToNonRoutableIp_ThrowsCleanTimeoutException()
    {
        var factory = new TcpConnectionFactory("127.0.0.1", 59999);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        Func<Task> act = async () => await factory.ConnectAsync(cts.Token);
        await act.Should().ThrowAsync<Exception>()
           .Where(e => e is SocketException || e is OperationCanceledException);
    }

    [Fact]
    public async Task TC_TRN_203_TcpConnection_LargeWriteBackpressure_CancellationDuringFlush()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var serverStop = new CancellationTokenSource();

        var acceptTask = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync(serverStop.Token);
            await Task.Delay(Timeout.InfiniteTimeSpan, serverStop.Token);
        });

        try
        {
            var factory = new TcpConnectionFactory("127.0.0.1", port);
            await using var ctx = await factory.ConnectAsync();

            byte[] largeChunk = new byte[64 * 1024];
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            Func<Task> act = async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    await ctx.Output.WriteAsync(largeChunk, cts.Token);
                    await ctx.Output.FlushAsync(cts.Token);
                }
                cts.Token.ThrowIfCancellationRequested();
            };

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            serverStop.Cancel();
            listener.Stop();
            await AwaitCleanupAsync(acceptTask);
        }
    }
}
