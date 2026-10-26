namespace Kable.Codecs;

using System;
using System.Buffers;
using Kable.Core.Checksums;

public sealed partial class ModbusRtuCodec
{
    private bool TryDecodeShortRead(ref ReadOnlySequence<byte> buffer, byte byteCount,
        out ReadOnlyMemory<byte> message)
    {
        int length = (int)buffer.Length;
        Span<byte> prefix = stackalloc byte[7];
        buffer.CopyTo(prefix);
        int responseLength = 5 + byteCount;
        bool responseAllowed = responseLength <= _maxFrameSize;

        if (responseAllowed && length >= responseLength &&
            Crc16Modbus.Validate(prefix.Slice(0, responseLength)))
        {
            message = prefix.Slice(0, responseLength).ToArray();
            buffer = buffer.Slice(responseLength);
            return true;
        }

        // At seven bytes, a read request already contains the CRC low byte.
        // A mismatch rules out an incomplete eight-byte request without waiting.
        bool requestCanComplete = _maxFrameSize >= 8 &&
            (length < 7 || (byte)Crc16Modbus.Compute(prefix.Slice(0, 6)) == prefix[6]);
        bool responseCanComplete = responseAllowed && length < responseLength;
        if (!requestCanComplete && !responseCanComplete)
        {
            buffer = buffer.Slice(1);
        }

        message = ReadOnlyMemory<byte>.Empty;
        return false;
    }
}
