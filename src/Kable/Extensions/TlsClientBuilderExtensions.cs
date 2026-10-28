namespace Kable.Extensions;

using Kable.Transports.Tls;

public static class TlsClientBuilderExtensions
{
    public static KableClientBuilder<T> UseTlsTcp<T>(this KableClientBuilder<T> builder, string host, int port,
        TlsClientOptions options, int connectTimeoutMs = 5000) =>
        builder.UseConnectionFactory(new TlsTcpConnectionFactory(host, port, options, connectTimeoutMs));
}
