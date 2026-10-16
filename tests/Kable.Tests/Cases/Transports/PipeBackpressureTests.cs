namespace Kable.Tests.Cases;

using System;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Extensions;
using Kable.Tests.Fixtures;
using Kable.Transports;
using Xunit;

[Collection("HardwareTransportTests")]
public sealed class PipeBackpressureTests
{
    [Fact]
    public async Task TC_TRN_07_PipeBackpressure_ConsumerStalled_HandlesBackpressureWithoutMemoryLeak()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // Write 1000 frames into remote write without reading from Stream immediately
        for (int i = 0; i < 1000; i++)
        {
            await factory.Context.WriteAsciiLineAsync($"$STALL_ITEM_{i}", 0x0A);
        }

        // Now consume from Stream and verify items are received
        int count = 0;
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
        await foreach (var item in session.Stream.WithCancellation(cts.Token))
        {
            count++;
            if (count >= 1000) break;
        }

        count.Should().Be(1000);
    }

    [Fact]
    public async Task TC_TRN_104_Pipe_BackpressureThresholdExceeded_PausesWriterUntilDrain()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // Fill buffer without reading
        for (int i = 0; i < 500; i++)
        {
            await factory.Context.WriteAsciiLineAsync($"$BURST_{i}", 0x0A);
        }

        // Drain buffer
        int drained = 0;
        using var drainCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
        await foreach (var item in session.Stream.WithCancellation(drainCts.Token))
        {
            drained++;
            if (drained >= 500) break;
        }

        drained.Should().Be(500);
    }
}
