namespace Kable.Tests.Cases.Codecs;

using System.Buffers;
using Kable.Codecs;
using Xunit;

public sealed class ModbusRtuFragmentRegressionTests
{
    [Fact]
    public void DiagnosticsFrameSurvivesEveryFragmentBoundary()
    {
        var codec = new ModbusRtuCodec();
        var writer = new ArrayBufferWriter<byte>();
        codec.Encode(new byte[] { 1, 8, 0, 0, 0x12, 0x34 }, writer);
        for (int i = 1; i < writer.WrittenCount; i++)
        {
            var partial = new ReadOnlySequence<byte>(writer.WrittenMemory.Slice(0, i));
            Assert.False(codec.TryDecode(ref partial, out _));
            Assert.Equal(i, partial.Length);
        }
        var full = new ReadOnlySequence<byte>(writer.WrittenMemory);
        Assert.True(codec.TryDecode(ref full, out _));
        Assert.True(full.IsEmpty);
    }
}
