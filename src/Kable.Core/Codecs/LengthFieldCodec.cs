namespace Kable.Codecs;

using System;
using System.Buffers;
using System.Buffers.Binary;
using Kable.Exceptions;

/// <summary>
/// 길이 필드 기반 패킷의 헤더 파싱 옵션
/// </summary>
public sealed class LengthFieldOptions
{
    /// <summary>
    /// 프레임 시작 매직 바이트(예: STX 0x02). null이면 시작 바이트 검사 생략
    /// </summary>
    public byte? HeaderMarker { get; init; }

    /// <summary>
    /// 프레임 시작점 기준 길이 필드의 시작 오프셋 (기본값: 0)
    /// </summary>
    public int LengthFieldOffset { get; init; } = 0;

    /// <summary>
    /// 길이 필드의 크기 (바이트 수: 1, 2, 4 지원)
    /// </summary>
    public int LengthFieldLength { get; init; } = 2;

    /// <summary>
    /// 길이 필드에 기록된 값에 헤더/길이필드 자체의 바이트 수가 이미 포함되어 있는지 여부
    /// false면 길이 필드 값 = 순수 페이로드 길이
    /// </summary>
    public bool LengthIncludesHeader { get; init; } = false;

    /// <summary>
    /// 페이로드 뒤에 붙는 고정 트레일러/체크섬/ETX 바이트 수 (기본값: 0)
    /// </summary>
    public int TrailerLength { get; init; } = 0;

    /// <summary>
    /// 빅엔디언 여부 (false면 리틀엔디언)
    /// </summary>
    public bool IsBigEndian { get; init; } = true;

    /// <summary>
    /// 최대 허용 프레임 크기 (OOM 방어)
    /// </summary>
    public int MaxFrameSize { get; init; } = 65536;

    /// <summary>
    /// 헤더 마커 불일치 시 다음 유효 헤더 마커까지 가비지 바이트를 자동으로 스킵하며 재동기화할지 여부
    /// </summary>
    public bool ResynchronizeOnInvalidHeader { get; init; } = true;
}

/// <summary>
/// 길이 필드 기반 바이너리/하이브리드 프로토콜 디코더 베이스 템플릿
/// 헤더 탐색, 선행 가비지 바이트 스킵(재동기화), OOM 방어, 완전 수신 전 무소비 규칙을 표준화합니다.
/// </summary>
public abstract class LengthFieldCodec<TMessage> : IProtocolCodec<TMessage>
{
    private readonly LengthFieldOptions _options;

    public virtual bool SupportsCorrelationId => false;
    public int MaxFrameSize => _options.MaxFrameSize;
    public LengthFieldOptions Options => _options;

    protected LengthFieldCodec(LengthFieldOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (_options.LengthFieldLength != 1 && _options.LengthFieldLength != 2 && _options.LengthFieldLength != 4)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "LengthFieldLength must be 1, 2, or 4 bytes.");
        }
        if (_options.LengthFieldOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "LengthFieldOffset must be non-negative.");
        }
    }

    public bool TryDecode(ref ReadOnlySequence<byte> buffer, out TMessage message)
    {
        while (true)
        {
            if (buffer.IsEmpty)
            {
                message = default!;
                return false;
            }

            // 1. 헤더 마커 검증 및 재동기화 (Garbage Skip)
            if (_options.HeaderMarker.HasValue)
            {
                var marker = _options.HeaderMarker.Value;
                var markerPos = buffer.PositionOf(marker);
                if (markerPos == null)
                {
                    // 버퍼 전체에 마커가 없음 -> 버퍼가 MaxFrameSize 초과 시 OOM 방어
                    if (buffer.Length > _options.MaxFrameSize)
                    {
                        buffer = buffer.Slice(buffer.End); // 전체 버리기
                        throw new ProtocolViolationException($"Frame size limit exceeded ({buffer.Length} > {_options.MaxFrameSize}) without valid header marker.");
                    }
                    message = default!;
                    return false;
                }

                // 마커 이전의 가비지 바이트 스킵
                if (!buffer.Start.Equals(markerPos.Value))
                {
                    if (!_options.ResynchronizeOnInvalidHeader)
                    {
                        throw new ProtocolViolationException("Invalid leading bytes before header marker.");
                    }
                    buffer = buffer.Slice(markerPos.Value);
                }
            }

            // 2. 최소 헤더 길이 확인 (오프셋 + 길이필드 길이)
            int minHeaderLength = _options.LengthFieldOffset + _options.LengthFieldLength;
            if (buffer.Length < minHeaderLength)
            {
                message = default!;
                return false;
            }

            // 3. 길이 필드 파싱 (Zero-Copy stackalloc)
            Span<byte> lengthSpan = stackalloc byte[_options.LengthFieldLength];
            var lengthFieldSeq = buffer.Slice(_options.LengthFieldOffset, _options.LengthFieldLength);
            lengthFieldSeq.CopyTo(lengthSpan);

            int rawLength = _options.LengthFieldLength switch
            {
                1 => lengthSpan[0],
                2 => _options.IsBigEndian ? BinaryPrimitives.ReadUInt16BigEndian(lengthSpan) : BinaryPrimitives.ReadUInt16LittleEndian(lengthSpan),
                4 => _options.IsBigEndian ? unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(lengthSpan)) : unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(lengthSpan)),
                _ => throw new InvalidOperationException()
            };

            // 4. 전체 프레임 크기 계산
            int totalFrameSize = _options.LengthIncludesHeader
                ? rawLength + _options.TrailerLength
                : minHeaderLength + rawLength + _options.TrailerLength;

            if (totalFrameSize < minHeaderLength + _options.TrailerLength || totalFrameSize > _options.MaxFrameSize)
            {
                // 불합리한 크기 -> 헤더 마커가 있는 경우 1바이트 스킵 후 다음 마커 재동기화 시도
                if (_options.HeaderMarker.HasValue && _options.ResynchronizeOnInvalidHeader)
                {
                    buffer = buffer.Slice(buffer.GetPosition(1, buffer.Start));
                    continue; // 다음 마커 탐색
                }
                throw new ProtocolViolationException($"Frame size limit exceeded: Invalid total frame size ({totalFrameSize}) exceeding limit ({_options.MaxFrameSize}).");
            }

            // 5. 프레임 전체 수신 확인 (미완성 시 커서 보존 및 대기)
            if (buffer.Length < totalFrameSize)
            {
                message = default!;
                return false;
            }

            // 6. 프레임 슬라이스 추출
            var frameSeq = buffer.Slice(0, totalFrameSize);

            // 7. 파생 클래스 디코딩 및 유효성 검증 (체크섬 등)
            if (!TryDecodePayload(frameSeq, out message))
            {
                // 디코딩/체크섬 실패 시
                if (_options.HeaderMarker.HasValue && _options.ResynchronizeOnInvalidHeader)
                {
                    buffer = buffer.Slice(buffer.GetPosition(1, buffer.Start));
                    continue; // 다음 마커로 재동기화
                }
                throw new ProtocolViolationException("Checksum or payload validation failed for frame.");
            }

            // 8. 성공 시 버퍼 Advance
            buffer = buffer.Slice(buffer.GetPosition(totalFrameSize, buffer.Start));
            return true;
        }
    }

    /// <summary>
    /// 완전하게 수신된 프레임 시퀀스를 도메인 메시지로 변환합니다.
    /// </summary>
    protected abstract bool TryDecodePayload(in ReadOnlySequence<byte> frameSequence, out TMessage message);

    public abstract void Encode(TMessage message, IBufferWriter<byte> output);
    public virtual string? ExtractCorrelationId(TMessage message) => null;
    public virtual bool IsAutonomousMessage(TMessage message) => false;
}
