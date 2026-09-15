namespace Kable.Codecs;

using System;
using System.Buffers;
using System.Text;

/// <summary>
/// ASCII 또는 커스텀 인코딩 기반 단일/다중 바이트 구분자 라인 코덱
/// <see cref="DelimitedFrameCodec{TMessage}"/> 베이스 클래스를 상속받아 강력한 가비지 스킵 및 OOM 방어를 공유합니다.
/// </summary>
public sealed class AsciiLineCodec : DelimitedFrameCodec<string>
{
    private readonly byte _delimiter;
    private readonly Encoding _encoding;

    public AsciiLineCodec(byte delimiter = 0x0A, Encoding? encoding = null, int maxFrameSize = 65536)
        : base(new DelimitedFrameOptions
        {
            StartMarker = null,
            EndDelimiter = new byte[] { delimiter },
            StripDelimiters = true,
            MaxFrameSize = maxFrameSize,
            ResynchronizeOnGarbage = false
        })
    {
        _delimiter = delimiter;
        _encoding = encoding ?? Encoding.ASCII;
    }

    public override bool TryDecode(ref ReadOnlySequence<byte> buffer, out string message)
    {
        if (base.TryDecode(ref buffer, out message))
        {
            return true;
        }

        message = string.Empty;
        return false;
    }

    protected override bool TryDecodePayload(in ReadOnlySequence<byte> payloadSequence, out string message)
    {
        message = GetStringFromSequence(payloadSequence).TrimEnd('\r', '\n');
        return true;
    }

    public override bool IsAutonomousMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        char first = message[0];
        return first == '$' || first == '#' || first == '!' || first == '*';
    }

    public override void Encode(string message, IBufferWriter<byte> output)
    {
        var bytes = _encoding.GetBytes(message);
        var span = output.GetSpan(bytes.Length + 1);
        bytes.CopyTo(span);
        span[bytes.Length] = _delimiter;
        output.Advance(bytes.Length + 1);
    }

    private string GetStringFromSequence(in ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
        {
#if NETCOREAPP || NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return _encoding.GetString(sequence.First.Span);
#else
            var segment = sequence.First;
            if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(segment, out var segmentArray))
            {
                return _encoding.GetString(segmentArray.Array!, segmentArray.Offset, segmentArray.Count);
            }
            return _encoding.GetString(segment.ToArray());
#endif
        }

        var length = unchecked((int)sequence.Length);
        byte[] rentArray = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            sequence.CopyTo(rentArray);
            return _encoding.GetString(rentArray, 0, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentArray);
        }
    }
}
