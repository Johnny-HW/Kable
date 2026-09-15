namespace Kable.Codecs;

using System;
using System.Buffers;
using System.Buffers.Binary;

/// <summary>
/// 2바이트 또는 4바이트 길이 접두사 바이너리 코덱
/// <see cref="LengthFieldCodec{TMessage}"/> 베이스 클래스를 상속받아 강력한 재동기화 및 엣지 케이스 방어를 공유합니다.
/// </summary>
public sealed class BinaryLengthPrefixedCodec : LengthFieldCodec<ReadOnlyMemory<byte>>
{
    private readonly int _headerLength;
    private readonly bool _isBigEndian;

    public BinaryLengthPrefixedCodec(int headerLength = 4, bool isBigEndian = false, int maxFrameSize = 65536)
        : base(new LengthFieldOptions
        {
            LengthFieldOffset = 0,
            LengthFieldLength = headerLength,
            LengthIncludesHeader = false,
            IsBigEndian = isBigEndian,
            MaxFrameSize = maxFrameSize
        })
    {
        _headerLength = headerLength;
        _isBigEndian = isBigEndian;
    }

    protected override bool TryDecodePayload(in ReadOnlySequence<byte> frameSequence, out ReadOnlyMemory<byte> message)
    {
        // frameSequence는 [헤더(2 or 4B)] + [바디]로 구성되어 있음
        var bodySequence = frameSequence.Slice(_headerLength);
        message = bodySequence.ToArray();
        return true;
    }

    public override void Encode(ReadOnlyMemory<byte> message, IBufferWriter<byte> output)
    {
        var span = output.GetSpan(_headerLength + message.Length);

        if (_headerLength == 2)
        {
            if (_isBigEndian)
            {
                BinaryPrimitives.WriteInt16BigEndian(span, (short)message.Length);
            }
            else
            {
                BinaryPrimitives.WriteInt16LittleEndian(span, (short)message.Length);
            }
        }
        else
        {
            if (_isBigEndian)
            {
                BinaryPrimitives.WriteInt32BigEndian(span, message.Length);
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(span, message.Length);
            }
        }

        message.Span.CopyTo(span.Slice(_headerLength));
        output.Advance(_headerLength + message.Length);
    }
}
