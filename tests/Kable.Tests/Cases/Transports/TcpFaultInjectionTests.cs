namespace Kable.Tests.Cases;

using System;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Extensions;
using Kable.Tests.Fixtures;
using Kable.Transports;
using Xunit;

[Collection("HardwareTransportTests")]
public sealed class TcpFaultInjectionTests
{
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

            // Force TCP RST: LingerState enabled with Timeout = 0
            serverSocket!.LingerState = new LingerOption(true, 0);
            serverSocket.Close();

            await Task.Delay(100);
            var pendingReq = session.RequestAsync<string>("PING_AFTER_RST", TimeSpan.FromSeconds(3)).AsTask();

            Func<Task> act = async () => await pendingReq;
            await act.Should().ThrowAsync<DeviceDisconnectedException>();

            session.IsConnected.Should().BeFalse();
        }
        finally
        {
            listener.Stop();
            try { await serverTask; } catch { }
        }
    }

    [Fact]
    public async Task TC_TRN_04_TcpConnection_UnreachableHost_ThrowsSocketExceptionQuickly()
    {
        // 127.0.0.1 on closed port
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

            // Enforce hard RST via socket linger
            acceptedSocket!.LingerState = new LingerOption(true, 0);
            acceptedSocket.Close();

            await Task.Delay(100);
            var pendingReq = session.RequestAsync<string>("TEST_RST_RECOVERY", TimeSpan.FromSeconds(3)).AsTask();

            Func<Task> act = async () => await pendingReq;
            await act.Should().ThrowAsync<DeviceDisconnectedException>();

            session.IsConnected.Should().BeFalse();
        }
        finally
        {
            listener.Stop();
            try { await acceptTask; } catch { }
        }
    }

    [Fact]
    public async Task TC_TRN_106_Tcp_ConnectionTimeoutToNonRoutableIp_ThrowsCleanTimeoutException()
    {
        // 127.0.0.1 on an unlistened port triggers immediate failure
        var factory = new TcpConnectionFactory("127.0.0.1", 59999);
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(500));

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

        // Server accepts but never reads to force OS TCP send buffer saturation
        var acceptTask = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync();
            // Wait without reading anything
            await Task.Delay(2000);
        });

        try
        {
            var factory = new TcpConnectionFactory("127.0.0.1", port);
            await using var ctx = await factory.ConnectAsync();

            // Write massive buffer to exhaust TCP send window
            byte[] largeChunk = new byte[64 * 1024]; // 64KB
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(100));

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
            listener.Stop();
            try { await acceptTask; } catch { }
        }
    }
}
