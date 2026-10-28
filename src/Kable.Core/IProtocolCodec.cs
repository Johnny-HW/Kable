namespace Kable.Codecs;

using System.Buffers;

public interface IProtocolCodec<TMessage>
{
    bool SupportsCorrelationId { get; }
    /// <summary>
    /// A decoded message must consume input. False may consume invalid bytes during resynchronization;
    /// callers must retry while progress is made and wait for input only when no bytes were consumed.
    /// Returned messages must own their data beyond the input buffer's lifetime.
    /// </summary>
    bool TryDecode(ref ReadOnlySequence<byte> buffer, out TMessage message);
    void Encode(TMessage message, IBufferWriter<byte> output);
    string? ExtractCorrelationId(TMessage message);
    bool IsAutonomousMessage(TMessage message);
}
