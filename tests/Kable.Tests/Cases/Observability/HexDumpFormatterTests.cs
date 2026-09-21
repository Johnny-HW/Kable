using System;
using System.Text;
using Kable.Observability;
using Xunit;

namespace Kable.Tests.Cases.Observability;

public sealed class HexDumpFormatterTests
{
    [Fact]
    public void Format_ShouldGenerateValidWiresharkStyleHexDump()
    {
        byte[] sample = Encoding.ASCII.GetBytes("Hello, Kable 1.3!");
        string dump = HexDumpFormatter.Format(sample);

        Assert.NotEmpty(dump);
        Assert.Contains("0000: 48 65 6C 6C 6F", dump);
        Assert.Contains("|Hello, Kable 1.3|", dump);
    }

    [Fact]
    public void Format_WithEmptySpan_ReturnsEmpty()
    {
        string dump = HexDumpFormatter.Format(ReadOnlySpan<byte>.Empty);
        Assert.Equal(string.Empty, dump);
    }

    [Fact]
    public void Format_WithMultiLineBuffer_AlignsOffsetsAndChars()
    {
        byte[] buffer = new byte[36];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(i + 0x20); // printable ASCII range
        }

        string dump = HexDumpFormatter.Format(buffer, bytesPerLine: 16);
        string[] lines = dump.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("0000:", lines[0]);
        Assert.StartsWith("0010:", lines[1]);
        Assert.StartsWith("0020:", lines[2]);
    }
}
