namespace Kable.Codecs;

using System;
using System.Buffers;
using Kable.Exceptions;

/// <summary>
/// 구분자(Delimiter/STX-ETX) 기반 프레임 파싱 옵션
/// </summary>
public sealed class DelimitedFrameOptions
{
    /// <summary>
    /// 프레임 시작 바이트 (예: STX 0x02). null이면 시작 바이트 검사 없이 종료 구분자만으로 프레이밍
    /// </summary>
    public byte? StartMarker { get; init; }

    /// <summary>
    /// 프레임 종료 구분자 시퀀스 (예: "\r\n", "\n", ETX 0x03 등)
    /// </summary>
    public byte[] EndDelimiter { get; init; } = new byte[] { 0x0A }; // 기본값 LF

    /// <summary>
    /// 반환 페이로드에서 시작 마커와 종료 구분자를 제거(Trim)할지 여부 (기본값: true)
    /// </summary>
    public bool StripDelimiters { get; init; } = true;

    /// <summary>
    /// 최대 허용 프레임 크기 (OOM 방어)
    /// </summary>
    public int MaxFrameSize { get; init; } = 65536;

    /// <summary>
    /// 시작 마커 이전의 깨진 바이트를 자동 스킵하며 재동기화할지 여부
    /// </summary>
    public bool ResynchronizeOnGarbage { get; init; } = true;
}

/// <summary>
/// 구분자(Delimiter) 기반 텍스트/바이너리 프로토콜 디코더 베이스 템플릿
/// 단일/다중 바이트 Delimiter, STX/ETX 프레이밍, 노이즈 바이트 스킵, OOM 한계 방어를 완벽 지원합니다.
/// </summary>
public abstract class DelimitedFrameCodec<TMessage> : IProtocolCodec<TMessage>
{
    private readonly DelimitedFrameOptions _options;

    public virtual bool SupportsCorrelationId => false;
    public int MaxFrameSize => _options.MaxFrameSize;
    public DelimitedFrameOptions Options => _options;

    protected DelimitedFrameCodec(DelimitedFrameOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (_options.EndDelimiter == null || _options.EndDelimiter.Length == 0)
        {
            throw new ArgumentException("EndDelimiter must not be empty.", nameof(options));
        }
    }

    public virtual bool TryDecode(ref ReadOnlySequence<byte> buffer, out TMessage message)
    {
        while (true)
        {
            if (buffer.IsEmpty)
            {
                message = default!;
                return false;
            }

            // 1. 시작 마커 검증 및 재동기화 (Garbage Skip)
            if (_options.StartMarker.HasValue)
            {
                byte startMarker = _options.StartMarker.Value;
                var startPos = buffer.PositionOf(startMarker);
                if (startPos == null)
                {
                    if (buffer.Length > _options.MaxFrameSize)
                    {
                        buffer = buffer.Slice(buffer.End); // 전체 비우기
                        throw new ProtocolViolationException($"Frame size limit exceeded ({buffer.Length} > {_options.MaxFrameSize}) without start marker.");
                    }
                    message = default!;
                    return false;
                }

                // 시작 마커 이전의 가비지 바이트 건너뛰기
                if (!buffer.Start.Equals(startPos.Value))
                {
                    if (!_options.ResynchronizeOnGarbage)
                    {
                        throw new ProtocolViolationException("Garbage bytes detected before start marker.");
                    }
                    buffer = buffer.Slice(startPos.Value);
                }
            }

            // 2. 종료 구분자 탐색 (단일 바이트 vs 다중 바이트)
            SequencePosition? endPos = null;
            int delimiterLength = _options.EndDelimiter.Length;

            if (delimiterLength == 1)
            {
                endPos = buffer.PositionOf(_options.EndDelimiter[0]);
            }
            else
            {
                endPos = FindMultiByteDelimiter(buffer, _options.EndDelimiter);
            }

            if (endPos == null)
            {
                // 종료 구분자가 아직 안 옴 -> 최대 크기 초과 확인
                if (buffer.Length > _options.MaxFrameSize)
                {
                    // 시작 마커가 있으면 1바이트 건너뛰어 다음 시작 마커 탐색
                    if (_options.StartMarker.HasValue && _options.ResynchronizeOnGarbage)
                    {
                        buffer = buffer.Slice(buffer.GetPosition(1, buffer.Start));
                        continue;
                    }
                    buffer = buffer.Slice(buffer.End);
                    throw new ProtocolViolationException($"Frame size limit exceeded ({buffer.Length} > {_options.MaxFrameSize}) without end delimiter.");
                }

                message = default!;
                return false;
            }

            // 3. 완전한 프레임 슬라이스 계산
            var endDelimiterEndPos = buffer.GetPosition(delimiterLength, endPos.Value);
            var fullFrameSeq = buffer.Slice(0, endDelimiterEndPos);

            if (fullFrameSeq.Length > _options.MaxFrameSize)
            {
                buffer = buffer.Slice(endDelimiterEndPos);
                throw new ProtocolViolationException($"Frame size limit exceeded ({fullFrameSeq.Length} > {_options.MaxFrameSize}).");
            }

            // 4. 페이로드 슬라이스 (Strip Delimiters 적용 여부)
            ReadOnlySequence<byte> payloadSeq;
            if (_options.StripDelimiters)
            {
                int startOffset = _options.StartMarker.HasValue ? 1 : 0;
                var payloadStart = buffer.GetPosition(startOffset, buffer.Start);
                payloadSeq = buffer.Slice(payloadStart, endPos.Value);
            }
            else
            {
                payloadSeq = fullFrameSeq;
            }

            // 5. 도메인 메시지 파싱 및 유효성 검증
            if (!TryDecodePayload(payloadSeq, out message))
            {
                // 파싱 실패 시 다음 시작 마커로 재동기화 시도
                if (_options.StartMarker.HasValue && _options.ResynchronizeOnGarbage)
                {
                    buffer = buffer.Slice(buffer.GetPosition(1, buffer.Start));
                    continue;
                }
                throw new ProtocolViolationException("Payload validation or decoding failed.");
            }

            // 6. 성공 시 버퍼 커서 전진
            buffer = buffer.Slice(endDelimiterEndPos);
            return true;
        }
    }

    private static SequencePosition? FindMultiByteDelimiter(in ReadOnlySequence<byte> sequence, byte[] delimiter)
    {
        byte first = delimiter[0];
        var remaining = sequence;
        Span<byte> matchSpan = stackalloc byte[delimiter.Length];

        while (!remaining.IsEmpty)
        {
            var firstPos = remaining.PositionOf(first);
            if (firstPos == null)
            {
                return null;
            }

            var candidateSeq = remaining.Slice(firstPos.Value);
            if (candidateSeq.Length < delimiter.Length)
            {
                return null; // 남은 길이가 구분자 길이보다 작으므로 더 이상 매칭 불가
            }

            candidateSeq.Slice(0, delimiter.Length).CopyTo(matchSpan);
            if (matchSpan.SequenceEqual(delimiter))
            {
                return firstPos.Value;
            }

            // 첫 바이트 다음부터 다시 검색
            remaining = candidateSeq.Slice(candidateSeq.GetPosition(1, candidateSeq.Start));
        }

        return null;
    }

    protected abstract bool TryDecodePayload(in ReadOnlySequence<byte> payloadSequence, out TMessage message);

    public abstract void Encode(TMessage message, IBufferWriter<byte> output);
    public virtual string? ExtractCorrelationId(TMessage message) => null;
    public virtual bool IsAutonomousMessage(TMessage message) => false;
}
