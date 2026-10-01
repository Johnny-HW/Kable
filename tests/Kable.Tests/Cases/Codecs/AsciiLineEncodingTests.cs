namespace Kable.Tests.Cases.Codecs;

using System.Buffers;
using System.Text;
using Kable.Codecs;
using Xunit;

public class AsciiLineEncodingTests
{
    [Theory]
    [InlineData("")]
    [InlineData("STATUS")]
    [InlineData("온도:24.5°C")]
    [InlineData("😀")]
    public void Encode_Utf8_WritesExactPayloadAndSingleDelimiter(string message)
    {
        var codec = new AsciiLineCodec(0x0A, Encoding.UTF8);
        var output = new ArrayBufferWriter<byte>();
        codec.Encode(message, output);
        Assert.Equal(Encoding.UTF8.GetBytes(message + "\n"), output.WrittenSpan.ToArray());
    }

    [Fact]
    public void Encode_ModernRuntime_DoesNotAllocateTemporaryArrays()
    {
        var codec = new AsciiLineCodec();
        var output = new ArrayBufferWriter<byte>(128);
        for (int i = 0; i < 10; i++) { output.Clear(); codec.Encode("STATUS", output); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { output.Clear(); codec.Encode("STATUS", output); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
