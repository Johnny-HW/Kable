namespace Kable.Simple;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Configuration;
using Kable.Extensions;

public static partial class KableSimple
{
    /// <summary>Opens a configured device. Simple options, when supplied, override the device's request default.</summary>
    public static async ValueTask<IKableSimpleClient> OpenAsync(KableDeviceOptions device,
        KableSimpleOptions? options = null, CancellationToken ct = default)
    {
        if (device == null) throw new ArgumentNullException(nameof(device));
        var configured = options ?? new KableSimpleOptions
        { DefaultTimeout = TimeSpan.FromMilliseconds(device.DefaultRequestTimeoutMs) };
        configured.Validate();
        var builder = new KableClientBuilder<string>().UseOptions(device)
            .UseCodec(new AsciiLineCodec(configured.Delimiter, configured.Encoding,
                isAutonomousPredicate: configured.IsAutonomousMessage));
        if (configured.Observer != null) builder.UseObserver(configured.Observer);
        var session = builder.Build();
        var client = new KableSimpleClient(session, configured.Observer, configured.DefaultTimeout);
        try
        {
            await client.InitializeAsync(ct).ConfigureAwait(false);
            return client;
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
