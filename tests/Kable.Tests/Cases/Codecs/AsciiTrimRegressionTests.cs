namespace Kable.Tests.Cases.Codecs;

using System.Buffers;
using System.Text;
using Kable.Codecs;
using Xunit;

public sealed class AsciiTrimRegressionTests
{
    [Theory]
    [InlineData("HELLO\r\n", "HELLO")]
    [InlineData("HELLO\n", "HELLO")]
    [InlineData("\r\r\n", "")]
    [InlineData("한글\r\n", "한글")]
    public void Utf8PreservesPayloadWhileTrimmingLineEnding(string wire, string expected)
    {
        var sequence = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(wire));
        Assert.True(new AsciiLineCodec(encoding: Encoding.UTF8).TryDecode(ref sequence, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CrLfDoesNotAllocateAnAdditionalStringPerDecode()
    {
        var codec = new AsciiLineCodec();
        byte[] lf = Encoding.ASCII.GetBytes("HELLO\n");
        byte[] crlf = Encoding.ASCII.GetBytes("HELLO\r\n");
        Measure(lf); Measure(crlf);
        Assert.Equal(Measure(lf), Measure(crlf));
        long Measure(byte[] bytes)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                var sequence = new ReadOnlySequence<byte>(bytes);
                codec.TryDecode(ref sequence, out _);
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
}
