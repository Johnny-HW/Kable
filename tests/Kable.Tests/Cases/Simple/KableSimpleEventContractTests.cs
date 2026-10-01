namespace Kable.Tests.Cases.Simple;

using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using Kable.Codecs;
using Kable.Core;
using Kable.Extensions;
using Kable.Simple;
using Kable.Transports;
using Xunit;

public class KableSimpleEventContractTests
{
    [Fact]
    public async Task QueryConsumesResponse_WhileAutonomousEventReachesSubscriber()
    {
        var (connection, remote) = InMemoryConnectionContext.CreatePair();
        await using var peer = remote;
        var session = new KableClientBuilder<string>().UseConnectionFactory(new Factory(connection))
            .UseCodec(new AsciiLineCodec()).Build();
        await using var client = await KableSimple.FromSessionAsync(session);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lines = new List<string>();
        client.LineReceived += text => { lines.Add(text); received.TrySetResult(text); };
        var query = client.QueryAsync("STATUS").AsTask();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var request = await remote.Input.ReadAsync(timeout.Token);
        remote.Input.AdvanceTo(request.Buffer.End);
        await remote.Output.WriteAsync(Encoding.ASCII.GetBytes("$EVENT\nOK\n"), timeout.Token);
        Assert.Equal("OK", await query.WaitAsync(timeout.Token));
        Assert.Equal("$EVENT", await received.Task.WaitAsync(timeout.Token));
        await client.DisposeAsync();
        Assert.Equal(new[] { "$EVENT" }, lines);
    }

    private sealed class Factory(IConnectionContext context) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(context);
    }
}
