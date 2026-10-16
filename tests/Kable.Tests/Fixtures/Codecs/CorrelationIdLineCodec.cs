namespace Kable.Tests.Fixtures.Codecs;

using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Tests.Fixtures;

internal sealed class CorrelationIdLineCodec : IProtocolCodec<string>
{
    private readonly byte _delimiter;

    public bool SupportsCorrelationId => true;

    public CorrelationIdLineCodec(byte delimiter = 0x0A)
    {
        _delimiter = delimiter;
    }

    public bool TryDecode(ref ReadOnlySequence<byte> buffer, out string message)
    {
        var pos = buffer.PositionOf(_delimiter);
        if (pos == null)
        {
            message = string.Empty;
            return false;
        }

        var slice = buffer.Slice(0, pos.Value);
        message = Encoding.ASCII.GetString(slice.ToArray()).TrimEnd('\r', '\n');
        buffer = buffer.Slice(buffer.GetPosition(1, pos.Value));
        return true;
    }

    public void Encode(string message, IBufferWriter<byte> output)
    {
        var bytes = Encoding.ASCII.GetBytes(message);
        var span = output.GetSpan(bytes.Length + 1);
        bytes.CopyTo(span);
        span[bytes.Length] = _delimiter;
        output.Advance(bytes.Length + 1);
    }

    public string? ExtractCorrelationId(string message)
    {
        int colonIdx = message.IndexOf(':');
        if (colonIdx > 0)
        {
            return message.Substring(0, colonIdx);
        }
        return null;
    }

    public bool IsAutonomousMessage(string message)
    {
        return message.StartsWith("$", StringComparison.Ordinal);
    }
}
