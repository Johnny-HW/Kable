namespace Kable.Tests.Cases.Integrations;

using System;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using global::Grpc.Net.Client;
using Kable.Codecs;
using Kable.Engine;
using Kable.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

public sealed class GrpcTransportIntegrationTests
{
    private static async Task<(IHost host, string address, KableTransportServiceImpl service)> StartGrpcServerAsync()
    {
        var service = new KableTransportServiceImpl();

        var host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, 0, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                });
                webBuilder.ConfigureServices(services =>
                {
                    services.AddGrpc();
                    services.AddSingleton(service);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGrpcService<KableTransportServiceImpl>();
                    });
                });
            })
            .Build();

        await host.StartAsync();
        var address = host.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .First();
        return (host, address, service);
    }

    [Fact]
    public async Task GrpcTunnel_ClientServerRoundTrip_TransfersDataSeamlessly()
    {
        var (host, address, serverService) = await StartGrpcServerAsync();
        using var hostScope = host;
        await using var serviceScope = serverService;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var clientFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverAcceptTask = Task.Run(async () =>
        {
            await using var serverCtx = await serverService.AcceptAsync(cts.Token);
            var readResult = await serverCtx.Input.ReadAsync(cts.Token);
            var received = Encoding.ASCII.GetString(System.Buffers.BuffersExtensions.ToArray(readResult.Buffer));
            serverCtx.Input.AdvanceTo(readResult.Buffer.End);

            var echo = "ECHO:" + received;
            await serverCtx.Output.WriteAsync(Encoding.ASCII.GetBytes(echo), cts.Token);
            await serverCtx.Output.FlushAsync(cts.Token);

            await Task.WhenAny(clientFinished.Task, Task.Delay(5000, cts.Token));
            return received;
        });

        try
        {
            var clientFactory = new GrpcClientFactory(address);
            await using var clientCtx = await clientFactory.ConnectAsync(cts.Token);

            var message = "HELLO_GRPC_PIPELINES\n";
            await clientCtx.Output.WriteAsync(Encoding.ASCII.GetBytes(message), cts.Token);
            await clientCtx.Output.FlushAsync(cts.Token);

            var clientReadResult = await clientCtx.Input.ReadAsync(cts.Token);
            var clientReceived = Encoding.ASCII.GetString(System.Buffers.BuffersExtensions.ToArray(clientReadResult.Buffer));
            clientCtx.Input.AdvanceTo(clientReadResult.Buffer.End);

            clientReceived.Should().Be("ECHO:HELLO_GRPC_PIPELINES\n");
            clientFinished.TrySetResult(true);

            var serverReceived = await serverAcceptTask;
            serverReceived.Should().Be("HELLO_GRPC_PIPELINES\n");
        }
        finally
        {
            clientFinished.TrySetResult(true);
            cts.Cancel();
            try { await serverAcceptTask; }
            catch (OperationCanceledException) { }
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task KableSession_OverGrpcTunnel_CompletesRequestAsyncSuccessfully()
    {
        var (host, address, serverService) = await StartGrpcServerAsync();
        using var hostScope = host;
        await using var serviceScope = serverService;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sessionFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            await using var ctx = await serverService.AcceptAsync(cts.Token);
            while (!cts.IsCancellationRequested)
            {
                var res = await ctx.Input.ReadAsync(cts.Token);
                var buf = res.Buffer;
                if (buf.Length > 0)
                {
                    var responseBytes = Encoding.ASCII.GetBytes("STATUS_OK\n");
                    await ctx.Output.WriteAsync(responseBytes, cts.Token);
                    await ctx.Output.FlushAsync(cts.Token);
                    ctx.Input.AdvanceTo(buf.End);
                    break;
                }
                if (res.IsCompleted || res.IsCanceled) break;
            }

            await Task.WhenAny(sessionFinished.Task, Task.Delay(5000, cts.Token));
        });

        var clientFactory = new GrpcClientFactory(address);
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(clientFactory, codec);
        await session.StartAsync(cts.Token);

        try
        {
            var response = await session.RequestAsync<string>("GET_STATUS", TimeSpan.FromSeconds(5), cts.Token);

            response.Should().Be("STATUS_OK");
            sessionFinished.TrySetResult(true);
        }
        finally
        {
            sessionFinished.TrySetResult(true);
            await session.StopAsync();
            cts.Cancel();
            try { await serverTask; }
            catch (OperationCanceledException) { }
            await host.StopAsync();
        }
    }
}
