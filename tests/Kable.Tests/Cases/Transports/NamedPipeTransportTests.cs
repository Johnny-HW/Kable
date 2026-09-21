namespace Kable.Tests.Cases;

using System;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Extensions;
using Xunit;

[Collection("HardwareTransportTests")]
public class NamedPipeTransportTests
{
    [Fact]
    public async Task NamedPipeTransport_LoopbackClientAndServer_TransmitsAndReceivesCorrectly()
    {
        string pipeName = "kable_test_pipe_" + Guid.NewGuid().ToString("N");

        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = Task.Run(async () =>
        {
            using var serverPipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await serverPipe.WaitForConnectionAsync(cts.Token);

            var buffer = new byte[128];
            int read = await serverPipe.ReadAsync(buffer, 0, buffer.Length, cts.Token);
            string received = Encoding.UTF8.GetString(buffer, 0, read);

            byte[] responseBytes = Encoding.UTF8.GetBytes("PIPE_ACK:" + received + "\n");
            await serverPipe.WriteAsync(responseBytes, 0, responseBytes.Length, cts.Token);
            await serverPipe.FlushAsync(cts.Token);
            await Task.Delay(Timeout.Infinite, cts.Token);
        }, cts.Token);

        try
        {
            await using var session = new KableClientBuilder<string>()
                .UseNamedPipe(pipeName, timeoutMs: 3000)
                .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
                .Build();

            await session.StartAsync(cts.Token);

            var response = await session.RequestAsync<string>("HELLO_IPC", TimeSpan.FromSeconds(3), cts.Token);

            response.Should().Be("PIPE_ACK:HELLO_IPC");
            session.IsConnected.Should().BeTrue();
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch { }
        }
    }
}
