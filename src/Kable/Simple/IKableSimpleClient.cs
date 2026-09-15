namespace Kable.Simple;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 초심자 및 빠른 장비 연동 프로토타이핑을 위한 경량 심플 클라이언트 인터페이스
/// 비동기(async/await) 우선 원칙을 따르며 이벤트 구독과 Request-Response Query 패턴을 직관적으로 제공합니다.
/// 세션 해제 시에는 비동기 스레드 블로킹 방지를 위해 <c>await using</c> 사용을 강력히 권장합니다.
/// </summary>
public interface IKableSimpleClient : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 현재 통신 세션 연결 여부
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 장비로부터 텍스트 라인이 수신되었을 때 발생하는 이벤트
    /// </summary>
    event Action<string>? LineReceived;

    /// <summary>
    /// 백그라운드 스트림 수신이나 이벤트 디스패치 중 예외가 발생했을 때 호출되는 이벤트
    /// </summary>
    event Action<Exception>? ErrorOccurred;

    /// <summary>
    /// 통신 단절 시 발생하는 이벤트. 정상 종료인 경우 null, 비정상 단절인 경우 원인 예외가 전달됩니다.
    /// </summary>
    event Action<Exception?>? Disconnected;

    /// <summary>
    /// 명령어를 전송하고 즉시 반환합니다. (개행문자는 자동으로 추가됨)
    /// </summary>
    ValueTask SendLineAsync(string command, CancellationToken ct = default);

    /// <summary>
    /// 명령어를 전송하고 장비로부터 단일 응답 라인이 올 때까지 대기하여 수신합니다.
    /// </summary>
    ValueTask<string> QueryAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default);

    /// <summary>
    /// 동기 리소스 해제 메서드입니다. Kable의 비차단 원칙에 따라 <see cref="IAsyncDisposable.DisposeAsync"/> 사용을 강력히 권장합니다.
    /// </summary>
    [Obsolete("동기 Dispose()는 Kable의 비차단 아키텍처에 위배될 수 있으므로 'await using' 또는 DisposeAsync()를 사용하십시오.")]
    new void Dispose();
}
