namespace Kable.Tests.Cases;

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Extensions;
using Kable.Observability;
using Kable.Transports;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("HardwareTransportTests")]
public class TcpListenerAndDiTests
{
    [Fact]
    public async Task TcpConnectionListener_AcceptsClientAndTransfersData()
    {
        await using var listener = new TcpConnectionListener(IPAddress.Loopback, 0);
        int port = ((IPEndPoint)listener.LocalEndPoint).Port;

        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = Task.Run(async () =>
        {
            await using var serverCtx = await listener.AcceptAsync();
            var readResult = await serverCtx.Input.ReadAsync(cts.Token);
            string msg = Encoding.UTF8.GetString(System.Buffers.BuffersExtensions.ToArray(readResult.Buffer));
            serverCtx.Input.AdvanceTo(readResult.Buffer.End);

            byte[] reply = Encoding.UTF8.GetBytes("SERVER_ACK:" + msg + "\n");
            await serverCtx.Output.WriteAsync(reply, cts.Token);
            await serverCtx.Output.FlushAsync(cts.Token);
            await Task.Delay(Timeout.Infinite, cts.Token);
        }, cts.Token);

        try
        {
            var clientFactory = new TcpConnectionFactory("127.0.0.1", port);
            await using var session = new KableClientBuilder<string>()
                .UseConnectionFactory(clientFactory)
                .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
                .Build();

            await session.StartAsync(cts.Token);

            var response = await session.RequestAsync<string>("HELLO_LISTENER", TimeSpan.FromSeconds(3), cts.Token);
            response.Should().Be("SERVER_ACK:HELLO_LISTENER");
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch { }
        }
    }

    [Fact]
    public async Task TC_TRN_107_TcpConnectionListener_Stop_ReleasesSocketAndAllowsPortReuse()
    {
        int port;
        {
            var listener = new TcpConnectionListener(IPAddress.Loopback, 0);
            port = ((IPEndPoint)listener.LocalEndPoint).Port;
            listener.Stop();
            await listener.DisposeAsync();
        }

        // Port must be immediately re-bindable without SocketException
        var rebindAction = () =>
        {
            var newListener = new TcpConnectionListener(IPAddress.Loopback, port);
            newListener.Stop();
        };

        rebindAction.Should().NotThrow<System.Net.Sockets.SocketException>();
    }

    [Fact]
    public void ServiceCollection_AddKableAndSession_ResolvesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddKable();
        services.AddKableSession<string>((builder, sp) =>
        {
            builder.UseTcp("127.0.0.1", 12345)
                   .UseCodec(new AsciiLineCodec());
        });

        using var provider = services.BuildServiceProvider();

        var observer = provider.GetService<ICommObserver>();
        observer.Should().NotBeNull();

        var session = provider.GetService<IDeviceSession<string>>();
        session.Should().NotBeNull();
    }

    [Fact]
    public void TC_GEN_103_Builder_MissingCodecOrFactory_ThrowsDescriptiveInvalidOperationException()
    {
        var builder = new KableClientBuilder<string>();

        Action actNoFactory = () => builder.Build();
        actNoFactory.Should().Throw<InvalidOperationException>()
                    .WithMessage("*ConnectionFactory must be configured*");

        Action actNoCodec = () => builder.UseTcp("127.0.0.1", 9000).Build();
        actNoCodec.Should().Throw<InvalidOperationException>()
                  .WithMessage("*ProtocolCodec must be configured*");
    }

    [Fact]
    public void TC_GEN_104_ServiceCollection_AddKableSession_ResolvesCorrectSingletonOrScoped()
    {
        var services = new ServiceCollection();
        services.AddKable();
        services.AddKableSession<string>((builder, sp) =>
        {
            builder.UseNamedPipe("di_pipe_test")
                   .UseCodec(new AsciiLineCodec());
        });

        using var provider = services.BuildServiceProvider();

        var session1 = provider.GetRequiredService<IDeviceSession<string>>();
        var session2 = provider.GetRequiredService<IDeviceSession<string>>();

        session1.Should().BeSameAs(session2, "Default AddKableSession should register session as Singleton");
    }
}
