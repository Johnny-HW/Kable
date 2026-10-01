namespace Kable.Simple;

using System;
using System.Text;
using Kable.Observability;

/// <summary>
/// KableSimple 파사드 클라이언트의 프로토콜 및 동작 구성을 지정하는 불변 옵션 레코드입니다.
/// 장비별 고유한 응답/이벤트 분류 규칙 및 타임아웃, 관측자를 손쉽게 설정할 수 있습니다.
/// </summary>
public sealed record KableSimpleOptions
{
    /// <summary>
    /// 메시지 프레임 구분자 바이트 (기본값: LF / 0x0A)
    /// </summary>
    public byte Delimiter { get; init; } = 0x0A;

    /// <summary>
    /// 문자열 인코딩 (기본값: null -> ASCII)
    /// </summary>
    public Encoding? Encoding { get; init; }

    /// <summary>
    /// QueryAsync 호출 시 기본 요청 제한시간 (기본값: 3초)
    /// </summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// 장비가 보낸 메시지가 요청의 응답이 아닌 자발 이벤트(Unsolicited Event/Alarm)인지 판별하는 사용자 정의 함수.
    /// null인 경우 Kable 기본 접두사 규칙($, #, !, *)을 사용합니다.
    /// 예를 들어 장비가 정상 응답으로 "!OK"를 사용한다면, msg => msg.StartsWith('$') 등으로 지정하여 "!OK"가 정상 응답으로 처리되도록 구성할 수 있습니다.
    /// </summary>
    public Func<string, bool>? IsAutonomousMessage { get; init; }

    /// <summary>
    /// 패킷 추적 및 관측용 옵저버 (선택적)
    /// </summary>
    public ICommObserver? Observer { get; init; }
}
