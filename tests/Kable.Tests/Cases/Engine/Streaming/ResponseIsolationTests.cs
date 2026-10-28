namespace Kable.Tests.Cases.Engine.Streaming;

using Kable.Codecs;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class ResponseIsolationTests
{
    [Fact]
    public async Task FullTelemetryStreamDoesNotDelayCommandReplyOrAlarm()
    {
        var link = new DesignLink();
        var signal = new TraceSignal();
        await using var session = new KableSession<string>(link, new AsciiLineCodec(), signal,
            sessionOptions: new KableSessionOptions<string> { InboundQueueCapacity = 1 });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await session.StartAsync(deadline.Token);
        await link.SendAsync("$DATA1\n$DATA2\n", deadline.Token);
        await signal.Streamed.Task.WaitAsync(deadline.Token);
        var pending = session.RequestAsync<string>("Q", TimeSpan.FromSeconds(1), deadline.Token).AsTask();
        await link.ReadRequestAsync(deadline.Token);
        await link.SendAsync("$ALARM:STOP\nREPLY\n", deadline.Token);
        Assert.Equal("REPLY", await pending);
        await using var alarm = session.GetAlarmsAsync(deadline.Token).GetAsyncEnumerator();
        Assert.True(await alarm.MoveNextAsync()); Assert.Equal("$ALARM:STOP", alarm.Current);
        await using var stream = session.Stream.GetAsyncEnumerator(deadline.Token);
        Assert.True(await stream.MoveNextAsync()); Assert.Equal("$DATA1", stream.Current);
        Assert.True(await stream.MoveNextAsync()); Assert.Equal("$DATA2", stream.Current);
    }
}
