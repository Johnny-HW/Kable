namespace Kable.Engine.Profiles;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 장비별 Enum 타입으로 Type-Safe하게 상시/수시 통신을 수행하는 클라이언트 인터페이스
/// </summary>
public interface IKableProfileClient<TPeriodic, TAperiodic> : IAsyncDisposable, IDisposable
    where TPeriodic : struct, Enum
    where TAperiodic : struct, Enum
{
    bool IsConnected { get; }

    /// <summary>
    /// 하드웨어 연결이 끊어지거나 비정상 종료되었을 때 발생하는 이벤트
    /// </summary>
    event Action<Exception?>? Disconnected;

    /// <summary>
    /// 상시 폴링 항목의 최신 응답 캐시값 즉시 조회 (Zero-Latency)
    /// </summary>
    string? GetLatest(TPeriodic command);

    /// <summary>
    /// 상시 폴링 항목의 최신 응답 캐시값 및 마지막 수신 시각을 함께 조회
    /// </summary>
    (string? Value, DateTime LastUpdatedUtc) GetLatestWithTimestamp(TPeriodic command);

    /// <summary>
    /// 지정된 유효 기간(maxAge) 이내에 갱신된 신선한 캐시값만 반환 (유효 기간 경과 시 false)
    /// </summary>
    bool TryGetFresh(TPeriodic command, TimeSpan maxAge, out string? value);

    /// <summary>
    /// 상시 폴링 데이터 수신 시 발생하는 일반 강한 참조 이벤트
    /// </summary>
    event Action<TPeriodic, string>? PeriodicDataReceived;

    /// <summary>
    /// UI(ViewModel/View) 메모리 누수를 원천 방지하는 약한 참조(Weak Reference) 기반 상시 데이터 구독.
    /// 대상 객체가 GC 수거되면 핸들러 참조가 자동 소멸되어 메모리 누수가 발생하지 않습니다.
    /// </summary>
    IDisposable SubscribeWeak<TTarget>(TTarget target, Action<TTarget, TPeriodic, string> handler) where TTarget : class;

    /// <summary>
    /// Zero-GC 할당: 컨텍스트 상태 객체(TState)를 함께 전달받아 외부 변수 캡처로 인한 클로저 힙 할당을 방지하는 오버로드
    /// </summary>
    IDisposable SubscribeWeak<TTarget, TState>(TTarget target, TState state, Action<TTarget, TState, TPeriodic, string> handler) where TTarget : class;

    /// <summary>
    /// 수시 명령 Enum 키 전송 및 응답 수신 (상시 폴링과 충돌 없이 자동 FIFO 조율)
    /// </summary>
    ValueTask<string> ExecuteAsync(TAperiodic command, TimeSpan? timeout = null, CancellationToken ct = default);

    /// <summary>
    /// 동적 파라미터가 필요한 경우(예: 좌표, 슬롯 번호 등) 수시 Enum 키 뒤에 인자 포맷팅을 덧붙여 전송
    /// </summary>
    ValueTask<string> ExecuteWithArgsAsync(TAperiodic command, string formatArgs, TimeSpan? timeout = null, CancellationToken ct = default);
}

/// <summary>
/// 문자열 기반 클라이언트 인터페이스
/// </summary>
public interface IKableProfileClient : IAsyncDisposable, IDisposable
{
    bool IsConnected { get; }

    /// <summary>
    /// 하드웨어 연결이 끊어지거나 비정상 종료되었을 때 발생하는 이벤트
    /// </summary>
    event Action<Exception?>? Disconnected;

    string? GetLatest(string periodicCommand);
    (string? Value, DateTime LastUpdatedUtc) GetLatestWithTimestamp(string periodicCommand);
    bool TryGetFresh(string periodicCommand, TimeSpan maxAge, out string? value);

    event Action<string, string>? PeriodicDataReceived;

    IDisposable SubscribeWeak<TTarget>(TTarget target, Action<TTarget, string, string> handler) where TTarget : class;
    IDisposable SubscribeWeak<TTarget, TState>(TTarget target, TState state, Action<TTarget, TState, string, string> handler) where TTarget : class;

    ValueTask<string> ExecuteAsync(string commandOrAlias, TimeSpan? timeout = null, CancellationToken ct = default);
}
