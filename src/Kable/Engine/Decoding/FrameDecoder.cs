namespace Kable.Engine.Decoding;

using System.Buffers;
using Kable.Codecs;
using Kable.Exceptions;

internal sealed class FrameDecoder<TMessage>
{
    private readonly IProtocolCodec<TMessage> _codec;
    internal FrameDecoder(IProtocolCodec<TMessage> codec) => _codec = codec;

    internal bool TryRead(ref ReadOnlySequence<byte> buffer, out TMessage message)
    {
        while (!buffer.IsEmpty)
        {
            long before = buffer.Length;
            bool decoded = _codec.TryDecode(ref buffer, out message);
            if (buffer.Length > before || (decoded && buffer.Length == before))
                throw new ProtocolViolationException("Decoder must consume input when returning a message and cannot grow the input buffer.");
            if (decoded) return true;
            if (buffer.Length == before) return false;
        }
        message = default!;
        return false;
    }
}
