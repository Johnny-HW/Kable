namespace Microsoft.Extensions.DependencyInjection;

using System;
using Kable.Codecs;
using Kable.Engine;
using Kable.Extensions;
using Kable.Observability;

public static class KableServiceExtensions
{
    public static IServiceCollection AddKable(this IServiceCollection services)
    {
        services.AddSingleton<ICommObserver, CommObserver>();
        return services;
    }

    public static IServiceCollection AddKableSession<TMessage>(
        this IServiceCollection services,
        Action<KableClientBuilder<TMessage>, IServiceProvider> configure)
    {
        services.AddSingleton<IDeviceSession<TMessage>>(sp =>
        {
            var builder = new KableClientBuilder<TMessage>();
            var observer = sp.GetService<ICommObserver>();
            if (observer != null)
            {
                builder.UseObserver(observer);
            }

            configure(builder, sp);
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// KableDeviceOptions 설정 모델을 주입하여 장비 세션을 DI 컨테이너에 등록합니다.
    /// </summary>
    public static IServiceCollection AddKableSession<TMessage>(
        this IServiceCollection services,
        Kable.Configuration.KableDeviceOptions options,
        Func<IServiceProvider, IProtocolCodec<TMessage>> codecFactory)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (codecFactory == null) throw new ArgumentNullException(nameof(codecFactory));

        return services.AddKableSession<TMessage>((builder, sp) =>
        {
            builder.UseOptions(options);
            builder.UseCodec(codecFactory(sp));
        });
    }
}
