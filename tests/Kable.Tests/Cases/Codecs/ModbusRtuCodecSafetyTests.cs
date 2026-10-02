namespace Kable.Tests.Cases.Codecs;

using System;
using System.Buffers;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core.Checksums;
using Xunit;

public class ModbusRtuCodecSafetyTests
{
    [Fact]
    public void TryDecode_FrameExceedingMaxFrameSize_ShouldRejectAndDropByte()
    {
        // Default maxFrameSize is 256
        var codec = new ModbusRtuCodec(maxFrameSize: 256);

        // FC 03 response with byteCount = 255 -> expectedLength = 3 + 255 + 2 = 260 (> 256)
        byte[] payload = new byte[260];
        payload[0] = 0x01; // Slave
        payload[1] = 0x03; // Read Holding Registers
        payload[2] = 0xFF; // ByteCount 255 -> Total frame size 260 > 256

        var seq = new ReadOnlySequence<byte>(payload);
        bool success = codec.TryDecode(ref seq, out var message);

        // Expected behavior: Must reject, not crash or stackalloc unbounded memory
        success.Should().BeFalse();
        message.IsEmpty.Should().BeTrue();
        seq.Length.Should().Be(259); // Drops 1 byte to attempt resynchronization
    }

    [Fact]
    public void TryDecode_LargeValidFrame_UnderLimit_DecodesUsingArrayPoolFallback()
    {
        // Custom limit: 512, stack limit is 256
        var codec = new ModbusRtuCodec(maxFrameSize: 512);

        // Frame with byteCount = 250 -> expectedLength = 3 + 250 + 2 = 255 (or 270)
        // Let's test byteCount = 260, expectedLength = 3 + 260 + 2 = 265 (> 256 stack limit, <= 512 max limit)
        byte[] raw = new byte[300];
        raw[0] = 0x01;
        raw[1] = 0x14; // Read File Record (or custom FC)
        for (int i = 2; i < 298; i++) raw[i] = (byte)(i % 250);
        Crc16Modbus.Append(raw.AsSpan(0, 298), raw.AsSpan());

        var seq = new ReadOnlySequence<byte>(raw);
        bool success = codec.TryDecode(ref seq, out var message);

        success.Should().BeTrue();
        message.Length.Should().Be(300);
        seq.Length.Should().Be(0);
    }
}
