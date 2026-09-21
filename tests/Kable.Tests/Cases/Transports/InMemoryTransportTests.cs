using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Engine;
using Kable.Extensions;
using Kable.Transports;
using Kable.Transports.Simulators;
using Xunit;

namespace Kable.Tests.Cases.Transports;

public sealed class InMemoryTransportTests
{
    [Fact]
    public async Task InMemory_ClientAndServer_ExchangeMessagesWithoutNetwork()
    {
        var (clientContext, serverContext) = InMemoryConnectionContext.CreatePair();

        // Server-side echo loop
        var serverTask = Task.Run(async () =>
        {
            var result = await serverContext.Input.ReadAsync();
            var received = Encoding.ASCII.GetString(BuffersExtensions.ToArray(result.Buffer));
            serverContext.Input.AdvanceTo(result.Buffer.End);

            byte[] reply = Encoding.ASCII.GetBytes($"REPLY:{received}\n");
            await serverContext.Output.WriteAsync(reply);
        });

        byte[] request = Encoding.ASCII.GetBytes("HELLO\n");
        await clientContext.Output.WriteAsync(request);

        await serverTask;

        var clientRead = await clientContext.Input.ReadAsync();
        string clientReceived = Encoding.ASCII.GetString(BuffersExtensions.ToArray(clientRead.Buffer));
        clientContext.Input.AdvanceTo(clientRead.Buffer.End);

        clientReceived.Should().Contain("REPLY:HELLO");

        await clientContext.DisposeAsync();
        await serverContext.DisposeAsync();
    }

    [Fact]
    public async Task UseSimulator_WithKableClientBuilder_HandlesRequests()
    {
        await using var session = new KableClientBuilder<string>()
            .UseSimulator(sim =>
            {
                sim.OnCommand("STATUS", "STATUS:READY")
                   .OnCommand("GET_TEMP", "TEMP:24.5");
            })
            .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
            .Build();

        await session.StartAsync();

        string status = await session.RequestAsync<string>("STATUS", TimeSpan.FromSeconds(2));
        status.Should().Be("STATUS:READY");

        string temp = await session.RequestAsync<string>("GET_TEMP", TimeSpan.FromSeconds(2));
        temp.Should().Be("TEMP:24.5");
    }
}
