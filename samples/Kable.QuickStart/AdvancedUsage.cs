namespace Kable.QuickStart;

using Kable.Codecs;
using Kable.Configuration;
using Kable.Core;
using Kable.Engine;
using Kable.Extensions;
using Kable.Simple;
using Kable.Simple.Reconnecting;

/// <summary>Compiled usage examples. RunConfiguredAsync is exercised by QuickStart and CI.</summary>
public static class AdvancedUsage
{
    public static async Task RunConfiguredAsync(CancellationToken ct = default)
    {
        await using var session = new KableClientBuilder<string>()
            .UseOptions(new KableDeviceOptions { DefaultRequestTimeoutMs = 1500 })
            .UseSimulator(sim => sim.OnCommand("STATUS", "READY").OnCommand("PING", "PONG"))
            .UseCodec(new AsciiLineCodec())
            .UseSessionOptions(new KableSessionOptions<string> { InboundQueueCapacity = 32 })
            .UseHeartbeat(new HeartbeatOptions<string>(TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5), () => "PING", message => message == "PONG"))
            .Build();
        await session.StartAsync(ct);
        string response = await session.RequestAsync("STATUS", ct: ct);
        if (response != "READY") throw new InvalidOperationException("Configured query failed.");
        Console.WriteLine($"[Configured default timeout] {response}");
    }

    public static async Task QueryWithRecoveryAsync(string host, int port, CancellationToken ct = default)
    {
        var simple = new KableSimpleOptions { ConnectTimeoutMs = 2000 };
        await using var client = await ReconnectingKableClient.OpenAsync(
            token => KableSimple.OpenTcpAsync(host, port, simple, token),
            new ReconnectOptions
            {
                MaxAttempts = 3, RetryDelay = TimeSpan.FromSeconds(1),
                ConnectTimeout = TimeSpan.FromSeconds(3)
            }, ct);
        client.LineReceived += Console.WriteLine;
        client.ErrorOccurred += error => Console.WriteLine(error.Message);
        client.Reconnected += () => Console.WriteLine("Connected with a fresh session.");
        // Errors are returned to the caller; the wrapper never replays this query.
        Console.WriteLine(await client.QueryAsync("STATUS", ct: ct));
    }
}
