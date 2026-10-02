namespace Kable.Tests.Cases.Codecs;

using System;
using System.Buffers;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core.Checksums;
using Xunit;

public class ModbusRtuCodecAllocationTests
{
    [Fact]
    public void TryDecode_IncompleteBuffer_ShouldAllocateZeroBytes()
    {
        var codec = new ModbusRtuCodec();
        byte[] raw = [0x01, 0x03]; // less than 4 bytes
        var seq = new ReadOnlySequence<byte>(raw);

        // Warmup
        codec.TryDecode(ref seq, out _);

        long startAlloc = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            var testSeq = new ReadOnlySequence<byte>(raw);
            codec.TryDecode(ref testSeq, out _);
        }
        long endAlloc = GC.GetAllocatedBytesForCurrentThread();

        (endAlloc - startAlloc).Should().Be(0);
    }

    [Fact]
    public void TryDecode_ExceedingLimit_ShouldAllocateZeroBytes()
    {
        var codec = new ModbusRtuCodec(maxFrameSize: 100);
        byte[] raw = new byte[150];
        raw[0] = 0x01;
        raw[1] = 0x03;
        raw[2] = 120; // 3 + 120 + 2 = 125 > 100
        var seq = new ReadOnlySequence<byte>(raw);

        // Warmup
        codec.TryDecode(ref seq, out _);

        long startAlloc = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            var testSeq = new ReadOnlySequence<byte>(raw);
            codec.TryDecode(ref testSeq, out _);
        }
        long endAlloc = GC.GetAllocatedBytesForCurrentThread();

        (endAlloc - startAlloc).Should().Be(0);
    }
}
