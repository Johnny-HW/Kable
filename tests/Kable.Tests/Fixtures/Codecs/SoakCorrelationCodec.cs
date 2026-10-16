namespace Kable.Tests.Fixtures.Codecs;

using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Tests.Fixtures;

internal sealed class SoakCorrelationCodec : DelimitedFrameCodec<string>
{
    public override bool SupportsCorrelationId => true;

    public SoakCorrelationCodec() : base(new DelimitedFrameOptions
    {
        EndDelimiter = new byte[] { 0x0A },
        StripDelimiters = true
    }) { }

    public override string? ExtractCorrelationId(string message)
    {
        if (string.IsNullOrEmpty(message)) return null;
        // Format: REQ:cid:... or RSP:cid:...
        if (message.StartsWith("REQ:") || message.StartsWith("RSP:"))
        {
            var parts = message.Split(':');
            if (parts.Length >= 2) return parts[1];
        }
        return null;
    }

    public override bool IsAutonomousMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        return message[0] == '$' || message.StartsWith("ALARM:");
    }

    protected override bool TryDecodePayload(in ReadOnlySequence<byte> payloadSequence, out string message)
    {
        message = Encoding.ASCII.GetString(BuffersExtensions.ToArray(payloadSequence));
        return true;
    }

    public override void Encode(string message, IBufferWriter<byte> output)
    {
        var bytes = Encoding.ASCII.GetBytes(message);
        var span = output.GetSpan(bytes.Length + 1);
        bytes.CopyTo(span);
        span[bytes.Length] = 0x0A;
        output.Advance(bytes.Length + 1);
    }
}
