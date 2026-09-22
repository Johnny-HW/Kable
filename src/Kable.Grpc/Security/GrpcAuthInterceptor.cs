namespace Kable.Grpc.Security;

using System;
using System.Threading.Tasks;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using Kable.Core.Security;
using Kable.Core.Security.Guards;

/// <summary>
/// gRPC 서버로 들어오는 모든 RPC 호출에 대해 토큰 기반 인증을 강제하는 서버 인터셉터입니다.
/// </summary>
public sealed class GrpcAuthInterceptor : Interceptor
{
    private readonly ITokenAuthValidator _validator;
    private readonly string _headerKey;

    public GrpcAuthInterceptor(SecurityTransportOptions options)
        : this(new TokenAuthValidator(options), options.HeaderKey)
    {
    }

    public GrpcAuthInterceptor(ITokenAuthValidator validator, string headerKey = "authorization")
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _headerKey = headerKey ?? "authorization";
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ValidateContext(context);
        return await continuation(request, context).ConfigureAwait(false);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ValidateContext(context);
        return await continuation(requestStream, context).ConfigureAwait(false);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ValidateContext(context);
        await continuation(request, responseStream, context).ConfigureAwait(false);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ValidateContext(context);
        await continuation(requestStream, responseStream, context).ConfigureAwait(false);
    }

    private void ValidateContext(ServerCallContext context)
    {
        var authHeader = context.RequestHeaders.GetValue(_headerKey);

        if (!_validator.ValidateHeader(authHeader))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication token is missing, expired, or invalid."));
        }
    }
}
