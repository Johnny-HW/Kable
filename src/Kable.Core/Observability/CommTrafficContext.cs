namespace Kable.Observability;

using System;
using System.Threading;

/// <summary>
/// 비동기 제어 흐름(AsyncLocal) 기반 통신 트래픽 성격(수시 명령 vs 주기 텔레메트리) 컨텍스트 스코프 관리자.
/// </summary>
public static class CommTrafficContext
{
    private static readonly AsyncLocal<TrafficKind?> _currentKind = new();

    public static TrafficKind CurrentKind => _currentKind.Value ?? TrafficKind.AperiodicCommand;

    public static IDisposable Use(TrafficKind kind)
    {
        var prev = _currentKind.Value;
        _currentKind.Value = kind;
        return new Scope(() => _currentKind.Value = prev);
    }

    private sealed class Scope : IDisposable
    {
        private Action? _dispose;
        public Scope(Action dispose) => _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
