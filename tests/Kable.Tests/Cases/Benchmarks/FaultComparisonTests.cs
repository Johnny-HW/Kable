using Kable.Comparison.Adapters;
using Kable.Comparison.Faults;
using Xunit;

namespace Kable.Tests.Cases.Benchmarks;

public class FaultComparisonTests
{
    [Fact]
    public async Task Late_reply_probe_detects_an_unsafe_adapter()
    {
        var row = await FaultProbes.RunAsync("DotNet", "raw", "late-reply", 1);
        Assert.Equal("WrongReply", row.Outcome);
        Assert.Equal("A", row.Detail);
    }

    [Theory]
    [InlineData("DotNet")]
    [InlineData("Kable")]
    [InlineData("TouchSocket")]
    [InlineData("SuperSocket")]
    [InlineData("DotNetty")]
    public async Task Common_policy_rejects_reuse_after_cancellation(string name)
    {
        var row = await FaultProbes.RunAsync(name, "guarded", "late-reply", 1);
        Assert.Equal("SafeRejected", row.Outcome);
    }

    [Theory]
    [InlineData("DotNet")]
    [InlineData("Kable")]
    [InlineData("TouchSocket")]
    [InlineData("SuperSocket")]
    [InlineData("DotNetty")]
    public async Task Common_policy_does_not_send_precanceled_commands(string name)
    {
        var row = await FaultProbes.RunAsync(name, "guarded", "pre-cancel", 1);
        Assert.Equal("Pass", row.Outcome);
        Assert.Equal(0, row.SentCommands);
    }

    [Theory]
    [InlineData("DotNet")]
    [InlineData("Kable")]
    [InlineData("TouchSocket")]
    [InlineData("SuperSocket")]
    [InlineData("DotNetty")]
    public async Task Common_policy_finishes_all_queued_calls_after_disconnect(string name)
    {
        var row = await FaultProbes.RunAsync(name, "guarded", "queued-disconnect", 1);
        Assert.Equal("Pass", row.Outcome);
        Assert.Contains("completed=16/16", row.Detail);
        Assert.Equal(1, row.SentCommands);
    }

    [Theory]
    [InlineData("DotNet")]
    [InlineData("Kable")]
    [InlineData("TouchSocket")]
    [InlineData("SuperSocket")]
    [InlineData("DotNetty")]
    public async Task Raw_adapters_also_check_cancellation_before_sending(string name)
    {
        var row = await FaultProbes.RunAsync(name, "raw", "pre-cancel", 1);
        Assert.Equal("Pass", row.Outcome);
        Assert.Equal(0, row.SentCommands);
    }

    [Fact]
    public async Task Report_counts_wrong_replies_as_failures_not_fast_successes()
    {
        var row = new FaultRow("test", "raw", "late-reply", 1,
            "WrongReply", "A", 1, 0, 2, "");
        Assert.False(row.Passed);
    }
}
