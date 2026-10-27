namespace Kable.SharedMemory.Tests.Transports;

using System.Buffers;
using System.IO.Pipelines;

internal static class TransportTestSupport
{
    internal static byte[] Pattern(int length, int seed = 17)
        => Enumerable.Range(0, length).Select(i => (byte)((i * 31 + i / 251 + seed) % 256)).ToArray();

    internal static async Task<byte[]> ReceiveAsync(PipeReader reader, int length, CancellationToken ct,
        Func<Task>? beforeFirstAdvance = null)
    {
        var received = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            var result = await reader.ReadAsync(ct);
            var buffer = result.Buffer;
            int count = (int)Math.Min(buffer.Length, length - offset);
            buffer.Slice(0, count).CopyTo(received.AsSpan(offset));
            if (offset == 0 && beforeFirstAdvance != null) await beforeFirstAdvance();
            offset += count;
            var consumed = buffer.GetPosition(count);
            reader.AdvanceTo(consumed, consumed);
            if (result.IsCompleted && offset < length)
                throw new EndOfStreamException($"Expected {length} bytes, received {offset}.");
        }
        return received;
    }

    internal static async Task SendAsync(PipeWriter writer, byte[] payload, int chunkSize, CancellationToken ct)
    {
        for (int offset = 0; offset < payload.Length; offset += chunkSize)
        {
            int count = Math.Min(chunkSize, payload.Length - offset);
            var result = await writer.WriteAsync(payload.AsMemory(offset, count), ct);
            if (result.IsCanceled || result.IsCompleted)
                throw new IOException("Output ended before the payload was sent.");
        }
    }
}
