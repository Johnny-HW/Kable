namespace Kable.Tests.Cases.Transports;

using System.Reflection;
using Kable.Codecs;
using Kable.Extensions;
using Kable.Transports.Simulators;
using Xunit;

public sealed class SimulatorOwnershipTests
{
    [Fact]
    public async Task SimulatorIsCreatedOnStartAndDisposedWithSession()
    {
        MockHardwareSimulator? simulator = null;
        var builder = new KableClientBuilder<string>().UseCodec(new AsciiLineCodec())
            .UseSimulator(sim => simulator = sim.OnCommand("PING", "PONG"));
        Assert.Null(simulator);
        await using var session = builder.Build();
        Assert.Null(simulator);
        await session.StartAsync();
        Assert.NotNull(simulator);
        Assert.Equal("PONG", await session.RequestAsync<string>("PING", TimeSpan.FromSeconds(3)));
        var loop = (Task)typeof(MockHardwareSimulator).GetField("_processingLoopTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(simulator)!;
        await session.DisposeAsync();
        Assert.True(loop.IsCompleted);
    }

    [Fact]
    public async Task BuilderCreatesIndependentSimulatorForEachSession()
    {
        var builder = new KableClientBuilder<string>().UseCodec(new AsciiLineCodec())
            .UseSimulator(sim => sim.OnCommand("PING", "PONG"));
        await using var first = builder.Build();
        await using var second = builder.Build();
        await first.StartAsync();
        await second.StartAsync();
        await first.DisposeAsync();
        Assert.Equal("PONG", await second.RequestAsync<string>("PING", TimeSpan.FromSeconds(3)));
    }
}
