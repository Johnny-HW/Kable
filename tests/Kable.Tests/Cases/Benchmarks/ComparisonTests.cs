using Kable.Comparison.Adapters;
using Kable.Comparison.Infrastructure;
using Xunit;

namespace Kable.Tests.Cases.Benchmarks;

public class ComparisonTests
{
    [Fact]
    public async Task Callback_framer_preserves_multiple_coalesced_and_partial_frames()
    {
        var inbox = new LineInbox();
        inbox.Receive("ONE\nTWO\nTH"u8);
        inbox.Receive("REE\n"u8);
        Assert.Equal("ONE", await inbox.ReadAsync(CancellationToken.None));
        Assert.Equal("TWO", await inbox.ReadAsync(CancellationToken.None));
        Assert.Equal("THREE", await inbox.ReadAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("Kable")]
    [InlineData("DotNet")]
    [InlineData("TouchSocket")]
    [InlineData("SuperSocket")]
    [InlineData("DotNetty")]
    public async Task Stalled_peer_respects_query_cancellation(string name)
    {
        using var connectDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var server = new EchoServer(stallOnRequest: true);
        await using var client = Clients.Create(name);
        await client.ConnectAsync(server.Port, connectDeadline.Token);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var query = client.QueryAsync("STALL", cancel.Token);
        var error = await Record.ExceptionAsync(async () =>
            await query.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.IsAssignableFrom<OperationCanceledException>(error);
    }

    [Theory]
    [InlineData("Kable")]
    [InlineData("DotNet")]
    [InlineData("TouchSocket")]
    [InlineData("SuperSocket")]
    [InlineData("DotNetty")]
    public async Task Concurrent_connections_produce_valid_measurements(string name)
    {
        var result = await Kable.Comparison.Scenarios.Measure.RunAsync(name, 1, 32, 16, 33, 2);
        Assert.Equal(48, result.Operations);
        Assert.True(result.P50Us <= result.P95Us);
        Assert.True(result.P95Us <= result.P99Us);
        Assert.True(result.RequestsPerSecond > 0);
        Assert.True(result.AllocatedBytesPerOperation > 0);
    }

    [Theory]
    [InlineData("Kable")]
    [InlineData("DotNet")]
    [InlineData("TouchSocket")]
    [InlineData("SuperSocket")]
    [InlineData("DotNetty")]
    public async Task All_clients_decode_fragmented_frames(string name)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var server = new EchoServer(fragment: true);
        await using var client = Clients.Create(name);
        await client.ConnectAsync(server.Port, deadline.Token);
        foreach (int size in new[] { 32, 256, 1024 })
        {
            string request = new('Q', size);
            Assert.Equal(request, await client.QueryAsync(request, deadline.Token));
        }
    }

    [Theory]
    [InlineData("Kable")]
    [InlineData("DotNet")]
    [InlineData("TouchSocket")]
    [InlineData("SuperSocket")]
    [InlineData("DotNetty")]
    public async Task Closed_peer_fails_pending_query(string name)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var server = new EchoServer(closeOnRequest: true);
        await using var client = Clients.Create(name);
        await client.ConnectAsync(server.Port, deadline.Token);
        var error = await Record.ExceptionAsync(async () =>
            await client.QueryAsync("CLOSE", deadline.Token));
        Assert.NotNull(error);
        Assert.False(deadline.IsCancellationRequested, "Peer close must fail before the watchdog.");
    }
}
