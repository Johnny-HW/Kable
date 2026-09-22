namespace Kable.Grpc.Security;

using System;
using System.Threading.Tasks;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using Kable.Core.Security;

/// <summary>
/// gRPC 서버 호출 진입 시 IConcurrencyGuard를 적용하여 동시 실행 및 재진입을 방지하는 서버 인터셉터입니다.
/// 이미 작업이 진행 중인 경우 StatusCode.FailedPrecondition 에러를 반환합니다.
/// </summary>
public sealed class GrpcConcurrencyInterceptor : Interceptor
{
    private readonly IConcurrencyGuard _guard;
    private readonly Func<ServerCallContext, string> _resourceKeySelector;

    public GrpcConcurrencyInterceptor(
        IConcurrencyGuard guard,
        Func<ServerCallContext, string>? resourceKeySelector = null)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _resourceKeySelector = resourceKeySelector ?? (ctx => ctx.Method);
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var key = _resourceKeySelector(context);
        var handle = await AcquireOrThrowAsync(key, context).ConfigureAwait(false);
        try
        {
            return await continuation(request, context).ConfigureAwait(false);
        }
        finally
        {
            if (handle != null) await handle.DisposeAsync().ConfigureAwait(false);
        }
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var key = _resourceKeySelector(context);
        var handle = await AcquireOrThrowAsync(key, context).ConfigureAwait(false);
        try
        {
            return await continuation(requestStream, context).ConfigureAwait(false);
        }
        finally
        {
            if (handle != null) await handle.DisposeAsync().ConfigureAwait(false);
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var key = _resourceKeySelector(context);
        var handle = await AcquireOrThrowAsync(key, context).ConfigureAwait(false);
        try
        {
            await continuation(request, responseStream, context).ConfigureAwait(false);
        }
        finally
        {
            if (handle != null) await handle.DisposeAsync().ConfigureAwait(false);
        }
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var key = _resourceKeySelector(context);
        var handle = await AcquireOrThrowAsync(key, context).ConfigureAwait(false);
        try
        {
            await continuation(requestStream, responseStream, context).ConfigureAwait(false);
        }
        finally
        {
            if (handle != null) await handle.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<IAsyncDisposable?> AcquireOrThrowAsync(string key, ServerCallContext context)
    {
        var handle = await _guard.TryAcquireAsync(key, context.CancellationToken).ConfigureAwait(false);
        if (handle == null)
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                $"Server resource '{key}' is currently busy executing another operation."));
        }
        return handle;
    }
}
