using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Core;
using Kable.Observability;
using Xunit;

namespace Kable.Tests.Cases.Observability;

public sealed class PacketReplayerTests
{
    [Fact]
    public async Task PacketReplayer_EmitsAllRecordsInOrder()
    {
        var now = DateTime.UtcNow;
        var records = new List<PacketTraceRecord>
        {
            new(now, PacketDirection.Tx, TrafficKind.AperiodicCommand, "CMD1", new byte[] { 0x01 }, "CMD1", TimeSpan.Zero),
            new(now.AddMilliseconds(20), PacketDirection.Rx, TrafficKind.AperiodicCommand, "RESP1", new byte[] { 0x02 }, "RESP1", TimeSpan.FromMilliseconds(20)),
            new(now.AddMilliseconds(40), PacketDirection.Tx, TrafficKind.AperiodicCommand, "CMD2", new byte[] { 0x03 }, "CMD2", TimeSpan.Zero)
        };

        var replayer = new PacketReplayer(records).WithSpeed(0); // Instant speed for unit testing
        var received = new List<PacketTraceRecord>();

        await replayer.ReplayAsync(rec =>
        {
            received.Add(rec);
            return Task.CompletedTask;
        });

        received.Should().HaveCount(3);
        received[0].Tag.Should().Be("CMD1");
        received[1].Tag.Should().Be("RESP1");
        received[2].Tag.Should().Be("CMD2");
    }
}
