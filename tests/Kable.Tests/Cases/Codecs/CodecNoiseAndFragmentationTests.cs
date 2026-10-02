namespace Kable.Tests.Cases.Codecs;

using System;
using System.Buffers;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core.Checksums;
using Xunit;

public class CodecNoiseAndFragmentationTests
{
    [Fact]
    public void ModbusRtuCodec_GarbagePreamble_RecoversAndDecodesValidFrame()
    {
        var codec = new ModbusRtuCodec();

        // 1. Prepare valid FC02 frame: Slave 1, FC 2, ByteCount 1, Data 3 + CRC
        byte[] validPayload = [0x01, 0x02, 0x01, 0x03];
        byte[] validFrame = new byte[validPayload.Length + 2];
        Crc16Modbus.Append(validPayload, validFrame);

        // 2. Prepend 50 bytes of garbage noise
        byte[] streamData = new byte[50 + validFrame.Length];
        for (int i = 0; i < 50; i++) streamData[i] = (byte)(0xAA ^ i);
        validFrame.CopyTo(streamData, 50);

        var seq = new ReadOnlySequence<byte>(streamData);

        // Act: Repeatedly call TryDecode until the garbage is skipped and the valid frame is found
        bool decoded = false;
        ReadOnlyMemory<byte> decodedMessage = ReadOnlyMemory<byte>.Empty;

        while (seq.Length > 0)
        {
            if (codec.TryDecode(ref seq, out decodedMessage))
            {
                decoded = true;
                break;
            }
        }

        decoded.Should().BeTrue();
        decodedMessage.Length.Should().Be(validFrame.Length);
        decodedMessage.Span.SequenceEqual(validFrame).Should().BeTrue();
        seq.Length.Should().Be(0);
    }

    [Fact]
    public void ModbusRtuCodec_ExtremeTrickling_1ByteIncrements_DecodesCorrectlyAtFullArrival()
    {
        var codec = new ModbusRtuCodec();

        byte[] validPayload = [0x01, 0x02, 0x01, 0x03];
        byte[] validFrame = new byte[validPayload.Length + 2];
        Crc16Modbus.Append(validPayload, validFrame);

        // Simulate 1 byte trickle
        for (int len = 1; len < validFrame.Length; len++)
        {
            var partialSeq = new ReadOnlySequence<byte>(validFrame.AsMemory(0, len));
            bool success = codec.TryDecode(ref partialSeq, out var msg);

            success.Should().BeFalse();
            msg.IsEmpty.Should().BeTrue();
            // Should not drop bytes when packet is simply incomplete
            partialSeq.Length.Should().Be(len);
        }

        // Full arrival
        var completeSeq = new ReadOnlySequence<byte>(validFrame);
        bool finalSuccess = codec.TryDecode(ref completeSeq, out var completeMsg);

        finalSuccess.Should().BeTrue();
        completeMsg.Length.Should().Be(validFrame.Length);
        completeSeq.Length.Should().Be(0);
    }
}
