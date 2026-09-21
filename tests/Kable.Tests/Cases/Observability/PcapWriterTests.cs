using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Observability;
using Xunit;

namespace Kable.Tests.Cases.Observability;

public sealed class PcapWriterTests
{
    [Fact]
    public async Task PcapWriter_WritesValidGlobalHeaderAndPacketRecords()
    {
        using var ms = new MemoryStream();
        var writer = new PcapWriter(ms);

        byte[] payload1 = Encoding.ASCII.GetBytes("PING\r\n");
        byte[] payload2 = Encoding.ASCII.GetBytes("PONG\r\n");

        await writer.WritePacketAsync(payload1, DateTime.UtcNow);
        await writer.WritePacketAsync(payload2, DateTime.UtcNow);

        byte[] bytes = ms.ToArray();

        // 1. Global Header: 24 bytes
        bytes.Length.Should().BeGreaterThan(24);
        uint magic = BitConverter.ToUInt32(bytes, 0);
        magic.Should().Be(0xA1B2C3D4); // PCAP standard magic

        ushort verMajor = BitConverter.ToUInt16(bytes, 4);
        ushort verMinor = BitConverter.ToUInt16(bytes, 6);
        verMajor.Should().Be(2);
        verMinor.Should().Be(4);

        // 2. First Packet Header: at offset 24 (16 bytes header + 6 bytes payload)
        uint capLen1 = BitConverter.ToUInt32(bytes, 24 + 8);
        capLen1.Should().Be((uint)payload1.Length);
    }
}
