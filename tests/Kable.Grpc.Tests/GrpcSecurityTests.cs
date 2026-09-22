namespace Kable.Grpc.Tests;

using System;
using System.Net;
using System.Net.Sockets;
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
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

public sealed class GrpcSecurityTests
{
    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task GrpcAuthInterceptor_RejectsUnauthenticatedCalls_AllowsValidToken()
    {
        var port = GetAvailablePort();
        var address = $"http://127.0.0.1:{port}";
        const string expectedToken = "secret-jwt-token-999";

        var authOptions = new SecurityTransportOptions
        {
            RequireTokenAuthentication = true,
            ValidTokens = { expectedToken }
        };

        var host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, port, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                });
                webBuilder.ConfigureServices(services =>
                {
                    services.AddGrpc(grpcOptions =>
                    {
                        grpcOptions.Interceptors.Add<GrpcAuthInterceptor>(authOptions);
                    });
                    services.AddSingleton<KableTransportServiceImpl>();
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

        try
        {
            using var channel = GrpcChannel.ForAddress(address);
            var rawClient = new KableTransportService.KableTransportServiceClient(channel);

            // 1. 인증 헤더 없이 호출 시 Unauthenticated 발생 검증
            var unauthCall = rawClient.StreamTunnel();
            var actUnauth = async () =>
            {
                await unauthCall.RequestStream.WriteAsync(new KablePacket());
                await unauthCall.RequestStream.CompleteAsync();
                await unauthCall.ResponseStream.MoveNext(CancellationToken.None);
            };

            var ex = await Assert.ThrowsAsync<RpcException>(actUnauth);
            ex.StatusCode.Should().Be(StatusCode.Unauthenticated);

            // 2. 유효한 토큰 인터셉터를 부착한 클라이언트로 호출 시 통과 검증
            var authInterceptor = GrpcSecurityExtensions.CreateClientAuthInterceptor(expectedToken);
            var authenticatedInvoker = channel.Intercept(authInterceptor);
            var authClient = new KableTransportService.KableTransportServiceClient(authenticatedInvoker);

            using var authCall = authClient.StreamTunnel();
            await authCall.RequestStream.WriteAsync(new KablePacket
            {
                Payload = Google.Protobuf.ByteString.CopyFrom(new byte[] { 1, 2, 3 })
            });

            // 연결 수립 정상 완료
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
        var port = GetAvailablePort();
        var address = $"http://127.0.0.1:{port}";

        var concurrencyGuard = new SingleFlightConcurrencyGuard(new ConcurrencyOptions
        {
            Mode = BusyHandlingMode.RejectImmediately
        });

        var host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, port, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                });
                webBuilder.ConfigureServices(services =>
                {
                    services.AddGrpc(grpcOptions =>
                    {
                        grpcOptions.Interceptors.Add<GrpcConcurrencyInterceptor>(concurrencyGuard);
                    });
                    services.AddSingleton<KableTransportServiceImpl>();
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

        try
        {
            using var channel = GrpcChannel.ForAddress(address);
            var client = new KableTransportService.KableTransportServiceClient(channel);

            // 1. 첫 번째 스트림 연결 수립 (스트림 유지로 Busy 상태 유발)
            using var firstCall = client.StreamTunnel();
            await firstCall.RequestStream.WriteAsync(new KablePacket
            {
                Payload = Google.Protobuf.ByteString.CopyFrom(new byte[] { 1 })
            });

            // 서버가 첫 번째 스트림을 수신하고 락을 잡을 때까지 잠시 대기
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

            // 2. 첫 번째 스트림이 열려 있는 상태에서 두 번째 호출 시도 -> FailedPrecondition 발생 검증
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

            // 3. 첫 번째 스트림 정상 종료
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
