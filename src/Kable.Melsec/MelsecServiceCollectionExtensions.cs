namespace Microsoft.Extensions.DependencyInjection;

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kable.Engine;
using Kable.Extensions;
using Kable.Melsec;
using Kable.Melsec.Codecs;
using Kable.Melsec.Protocol;
using Kable.Transports;

public static class MelsecServiceCollectionExtensions
{
    public static IServiceCollection AddKableMelsec(
        this IServiceCollection services,
        Action<MelsecOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<IMelsecPlcClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MelsecOptions>>().Value;
            var logger = sp.GetService<ILogger<MelsecPlcClient>>();

            var factory = new TcpConnectionFactory(options.Host, options.Port);
            var codec = new MelsecSlmpCodec();
            var session = new KableSession<Slmp3EFrame>(factory, codec);

            return new MelsecPlcClient(session, sp.GetRequiredService<IOptions<MelsecOptions>>(), logger);
        });


        return services;
    }
}
