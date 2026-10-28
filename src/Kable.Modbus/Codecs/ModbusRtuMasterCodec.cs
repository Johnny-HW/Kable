namespace Kable.Modbus.Codecs;

using System.Buffers;
using System.IO;
using Kable.Codecs;
using Kable.Core.Checksums;
using Kable.Modbus.Messages;

/// <summary>Response-only RTU framing. Reuses Modbus message fields, but never transmits MBAP headers.</summary>
public sealed class ModbusRtuMasterCodec : IProtocolCodec<ModbusTcpMessage>
{
    private ModbusTcpMessage? _request;
    public bool SupportsCorrelationId => false;
    public string? ExtractCorrelationId(ModbusTcpMessage message) => null;
    public bool IsAutonomousMessage(ModbusTcpMessage message) => false;

    public void Encode(ModbusTcpMessage message, IBufferWriter<byte> output)
    {
        if (message.ProtocolId != 0 || message.UnitId is < 1 or > 247 || message.Payload.Length is < 4 or > 252 ||
            message.FunctionCode is not (1 or 2 or 3 or 4 or 5 or 6 or 15 or 16))
            throw new ArgumentException("Invalid RTU request.", nameof(message));
        var span = output.GetSpan(message.Payload.Length + 4);
        span[0] = message.UnitId;
        span[1] = message.FunctionCode;
        message.Payload.Span.CopyTo(span[2..]);
        ushort crc = Crc16Modbus.Compute(span[..(message.Payload.Length + 2)]);
        span[message.Payload.Length + 2] = (byte)crc;
        span[message.Payload.Length + 3] = (byte)(crc >> 8);
        Volatile.Write(ref _request, message);
        output.Advance(message.Payload.Length + 4);
    }

    public bool TryDecode(ref ReadOnlySequence<byte> buffer, out ModbusTcpMessage message)
    {
        message = null!;
        if (buffer.Length < 3) return false;
        Span<byte> header = stackalloc byte[3];
        buffer.Slice(0, 3).CopyTo(header);
        byte unit = header[0], function = header[1];
        int normalFunction = function & 0x7F;
        if (unit is < 1 or > 247 || normalFunction is not (1 or 2 or 3 or 4 or 5 or 6 or 15 or 16))
            throw new InvalidDataException("Invalid RTU response address or function.");

        var request = Volatile.Read(ref _request);
        if (request != null && (unit != request.UnitId || normalFunction != request.FunctionCode))
            throw new InvalidDataException("RTU response does not match the outstanding request.");

        bool exception = (function & 0x80) != 0;
        int length = exception ? 5 : normalFunction <= 4 ? 5 + header[2] : 8;
        if (length > 256 || (!exception && normalFunction <= 4 &&
            (header[2] == 0 || header[2] > 250 || (normalFunction is 3 or 4 && header[2] % 2 != 0))))
            throw new InvalidDataException("Invalid RTU response byte count.");
        if (buffer.Length < length) return false;

        Span<byte> frame = stackalloc byte[256];
        buffer.Slice(0, length).CopyTo(frame);
        if (!Crc16Modbus.Validate(frame[..length]))
            throw new InvalidDataException("RTU response CRC mismatch.");

        if (request != null && !exception)
        {
            if (normalFunction <= 4)
            {
                var payload = request.Payload.Span;
                int quantity = (payload[2] << 8) | payload[3];
                int expectedBytes = normalFunction <= 2 ? (quantity + 7) / 8 : quantity * 2;
                if (header[2] != expectedBytes) throw new InvalidDataException("RTU response quantity mismatch.");
            }
            else if (!frame.Slice(2, 4).SequenceEqual(request.Payload.Span[..4]))
                throw new InvalidDataException("RTU write acknowledgement mismatch.");
        }

        // Own payload storage beyond PipeReader.AdvanceTo; CRC is deliberately not part of the PDU.
        message = new ModbusTcpMessage(request?.TransactionId ?? 0, unit, function, frame.Slice(2, length - 4).ToArray());
        buffer = buffer.Slice(length);
        return true;
    }
}
