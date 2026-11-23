namespace Kable.Grpc.Security;

using System;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;

/// <summary>
/// gRPC 클라이언트 및 서버에 보안(인증 토큰, 동시성 인터셉터) 설정을 지원하는 확장 메서드입니다.
/// </summary>
public static class GrpcSecurityExtensions
{
    /// <summary>
    /// 지정된 인증 토큰을 모든 gRPC 클라이언트 요청 헤더에 자동으로 주입하는 인터셉터를 생성합니다.
    /// </summary>
    public static Interceptor CreateClientAuthInterceptor(string token, string headerKey = "authorization")
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be null or whitespace.", nameof(token));
        }

        var normalizedHeader = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? token
            : $"Bearer {token}";

        return new ClientAuthHeaderInterceptor(headerKey, normalizedHeader);
    }

    private sealed class ClientAuthHeaderInterceptor : Interceptor
    {
        private readonly string _headerKey;
        private readonly string _headerValue;

        public ClientAuthHeaderInterceptor(string headerKey, string headerValue)
        {
            _headerKey = headerKey;
            _headerValue = headerValue;
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            var newContext = WithInjectedHeaders(context);
            return continuation(request, newContext);
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            var newContext = WithInjectedHeaders(context);
            return continuation(request, newContext);
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            var newContext = WithInjectedHeaders(context);
            return continuation(newContext);
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            var newContext = WithInjectedHeaders(context);
            return continuation(request, newContext);
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            var newContext = WithInjectedHeaders(context);
            return continuation(newContext);
        }

        private ClientInterceptorContext<TRequest, TResponse> WithInjectedHeaders<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context)
            where TRequest : class
            where TResponse : class
        {
            var options = InjectMetadata(context.Options);
            return new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);
        }

        private CallOptions InjectMetadata(CallOptions options)
        {
            var headers = new Metadata();
            if (options.Headers != null)
            {
                foreach (var entry in options.Headers)
                {
                    if (entry.IsBinary)
                    {
                        headers.Add(entry.Key, entry.ValueBytes);
                    }
                    else
                    {
                        headers.Add(entry.Key, entry.Value);
                    }
                }
            }
            headers.Add(_headerKey, _headerValue);
            return options.WithHeaders(headers);
        }
    }
}
