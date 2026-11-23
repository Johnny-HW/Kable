namespace Kable.Grpc.Tests;

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using global::Grpc.Net.Client;
using Kable.Core.Security;
using Kable.Core.Security.Guards;
using Kable.Grpc;
using Kable.Grpc.Protos;
using Kable.Grpc.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

public sealed class GrpcSecurityTests
{
    private static async Task<(IHost Host, string Address)> StartHostAsync(Action<IServiceCollection> configureServices)
    {
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
                webBuilder.ConfigureServices(configureServices);
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
        return (host, address);
    }

    [Fact]
    public async Task GrpcAuthInterceptor_RejectsUnauthenticatedCalls_AllowsValidToken()
    {
        const string expectedToken = "secret-jwt-token-999";

        var authOptions = new SecurityTransportOptions
        {
            RequireTokenAuthentication = true,
            ValidTokens = { expectedToken }
        };

        var (host, address) = await StartHostAsync(services =>
        {
            services.AddGrpc(grpcOptions =>
            {
                grpcOptions.Interceptors.Add<GrpcAuthInterceptor>(authOptions);
            });
            services.AddSingleton<KableTransportServiceImpl>();
        });

        try
        {
            using var channel = GrpcChannel.ForAddress(address);
            var rawClient = new KableTransportService.KableTransportServiceClient(channel);

            using var unauthCall = rawClient.StreamTunnel();
            var actUnauth = async () =>
            {
                await unauthCall.RequestStream.WriteAsync(new KablePacket());
                await unauthCall.RequestStream.CompleteAsync();
                await unauthCall.ResponseStream.MoveNext(CancellationToken.None);
            };

            var ex = await Assert.ThrowsAsync<RpcException>(actUnauth);
            ex.StatusCode.Should().Be(StatusCode.Unauthenticated);

            var authInterceptor = GrpcSecurityExtensions.CreateClientAuthInterceptor(expectedToken);
            var authenticatedInvoker = channel.Intercept(authInterceptor);
            var authClient = new KableTransportService.KableTransportServiceClient(authenticatedInvoker);

            using var authCall = authClient.StreamTunnel();
            await authCall.RequestStream.WriteAsync(new KablePacket
            {
                Payload = Google.Protobuf.ByteString.CopyFrom(new byte[] { 1, 2, 3 })
            });

            await authCall.RequestStream.CompleteAsync();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Fact]
    public async Task GrpcConcurrencyInterceptor_RejectsConcurrentCallsWhenBusy()
    {
        var concurrencyGuard = new SingleFlightConcurrencyGuard(new ConcurrencyOptions
        {
            Mode = BusyHandlingMode.RejectImmediately
        });

        var (host, address) = await StartHostAsync(services =>
        {
            services.AddGrpc(grpcOptions =>
            {
                grpcOptions.Interceptors.Add<GrpcConcurrencyInterceptor>(concurrencyGuard);
            });
            services.AddSingleton<KableTransportServiceImpl>();
        });

        try
        {
            using var channel = GrpcChannel.ForAddress(address);
            var client = new KableTransportService.KableTransportServiceClient(channel);

            using var firstCall = client.StreamTunnel();
            await firstCall.RequestStream.WriteAsync(new KablePacket
            {
                Payload = Google.Protobuf.ByteString.CopyFrom(new byte[] { 1 })
            });

            var busy = false;
            for (int i = 0; i < 50; i++)
            {
                if (concurrencyGuard.IsBusy("/kable.transport.KableTransportService/StreamTunnel"))
                {
                    busy = true;
                    break;
                }
                await Task.Delay(20);
            }
            busy.Should().BeTrue();

            using var secondCall = client.StreamTunnel();
            var actSecond = async () =>
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await secondCall.RequestStream.WriteAsync(new KablePacket
                {
                    Payload = Google.Protobuf.ByteString.CopyFrom(new byte[] { 2 })
                }, timeoutCts.Token);
                await secondCall.RequestStream.CompleteAsync();
                await secondCall.ResponseStream.MoveNext(timeoutCts.Token);
            };

            var ex = await Assert.ThrowsAsync<RpcException>(actSecond);
            ex.StatusCode.Should().Be(StatusCode.FailedPrecondition);
            ex.Status.Detail.Should().Contain("busy executing another operation");

            await firstCall.RequestStream.CompleteAsync();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
            await concurrencyGuard.DisposeAsync();
        }
    }
}
