namespace Kable.Tests.Cases.Engine;

using System.IO.Pipelines;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class HeartbeatFailureTests
{
    [Fact]
    public async Task BlockedPingFlush_DoesNotStopWatchdog()
    {
        var factory = new TestMemoryConnectionFactory(outPipeOptions: new PipeOptions(pauseWriterThreshold: 1, resumeWriterThreshold: 1));
        var options = new HeartbeatOptions<string>(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(150), () => "PING");
        await using var session = new KableSession<string>(factory, new AsciiLineCodec(), heartbeatOptions: options);
        await session.StartAsync();
        await factory.Context.FlushStarted.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForDisconnectAsync(session);
    }

    [Fact]
    public async Task PingFactoryFailure_ClosesSession()
    {
        var factory = new TestMemoryConnectionFactory();
        var options = new HeartbeatOptions<string>(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(10), () => throw new InvalidOperationException("Ping failed"));
        await using var session = new KableSession<string>(factory, new AsciiLineCodec(), heartbeatOptions: options);
        await session.StartAsync();
        await WaitForDisconnectAsync(session);
    }

    private static async Task WaitForDisconnectAsync(KableSession<string> session)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (session.IsConnected) await Task.Delay(10, deadline.Token);
        Assert.False(session.IsConnected);
    }
}
