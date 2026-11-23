namespace Kable.Grpc.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using Kable.Grpc;
using Kable.Grpc.Security;
using Xunit;

public sealed class GrpcHardeningTests
{
    [Fact]
    public void ClientAuthInterceptor_ClonesHeadersWithoutMutatingCallerMetadata_AndCoversAllCallTypes()
    {
        var interceptor = GrpcSecurityExtensions.CreateClientAuthInterceptor("secret-token");
        var sharedHeaders = new Metadata { { "x-custom", "v1" } };
        var method = new Method<string, string>(
            MethodType.Unary, "Svc", "Op",
            Marshallers.Create(s => System.Text.Encoding.UTF8.GetBytes(s), b => System.Text.Encoding.UTF8.GetString(b)),
            Marshallers.Create(s => System.Text.Encoding.UTF8.GetBytes(s), b => System.Text.Encoding.UTF8.GetString(b)));

        Metadata? captured1 = null;
        Metadata? captured2 = null;
        var ctx = new ClientInterceptorContext<string, string>(method, "localhost", new CallOptions(sharedHeaders));

        interceptor.BlockingUnaryCall("req", ctx, (req, nextCtx) => { captured1 = nextCtx.Options.Headers; return "ok"; });
        interceptor.BlockingUnaryCall("req", ctx, (req, nextCtx) => { captured2 = nextCtx.Options.Headers; return "ok"; });

        // Caller's shared Metadata must remain unmutated.
        Assert.Single(sharedHeaders);
        Assert.Equal(2, captured1!.Count);
        Assert.Equal(2, captured2!.Count);
        Assert.Single(captured1, h => h.Key == "authorization");
        Assert.Single(captured2, h => h.Key == "authorization");

        Metadata? clientStreamCaptured = null;
        interceptor.AsyncClientStreamingCall(ctx, nextCtx => { clientStreamCaptured = nextCtx.Options.Headers; return null!; });
        Assert.Single(clientStreamCaptured!, h => h.Key == "authorization");

        Metadata? serverStreamCaptured = null;
        interceptor.AsyncServerStreamingCall("req", ctx, (req, nextCtx) => { serverStreamCaptured = nextCtx.Options.Headers; return null!; });
        Assert.Single(serverStreamCaptured!, h => h.Key == "authorization");
    }

    [Fact]
    public async Task GrpcClientFactory_ConnectAsync_RespectsPreCancelledToken()
    {
        var factory = new GrpcClientFactory("http://127.0.0.1:50051");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await factory.ConnectAsync(cts.Token));
    }

    [Fact]
    public async Task KableTransportServiceImpl_DisposeAsync_IsIdempotentAndCancelsAccept()
    {
        var svc = new KableTransportServiceImpl();
        await svc.DisposeAsync();
        await svc.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await svc.AcceptAsync());
    }
}
