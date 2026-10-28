namespace Kable.Tests.Cases.Engine.Messaging;

using Kable.Codecs;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class ResponseClassificationTests
{
    [Theory]
    [InlineData("NO_FAULT")]
    [InlineData("ALARM_CLOCK_READY")]
    [InlineData("EMERGENCY_CONTACT=123")]
    public async Task NormalResponseTextDoesNotDetermineRouting(string reply)
    {
        var link = new DesignLink();
        await using var session = new KableSession<string>(link, new AsciiLineCodec());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await session.StartAsync(deadline.Token);
        var pending = session.RequestAsync<string>("STATUS?", TimeSpan.FromSeconds(1), deadline.Token).AsTask();
        await link.ReadRequestAsync(deadline.Token);
        await link.SendAsync(reply + "\n", deadline.Token);
        Assert.Equal(reply, await pending);
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task ExplicitClassifierKeepsAlarmOutOfPendingResponse()
    {
        var link = new DesignLink();
        await using var session = new KableSession<string>(link, new AsciiLineCodec(isAutonomousPredicate: _ => false),
            new KableSessionOptions<string> { IsAlarmMessage = text => text.StartsWith("EVENT:") });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await session.StartAsync(deadline.Token);
        var pending = session.RequestAsync<string>("STATUS?", TimeSpan.FromSeconds(1), deadline.Token).AsTask();
        await link.ReadRequestAsync(deadline.Token);
        await link.SendAsync("EVENT:STOP\nREADY\n", deadline.Token);
        Assert.Equal("READY", await pending);
        await using var alarms = session.GetAlarmsAsync(deadline.Token).GetAsyncEnumerator();
        Assert.True(await alarms.MoveNextAsync()); Assert.Equal("EVENT:STOP", alarms.Current);
    }
}
