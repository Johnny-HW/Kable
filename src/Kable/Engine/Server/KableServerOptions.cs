namespace Kable.Engine;

using System;

/// <summary>
/// 1:N 서버 실행 옵션
/// </summary>
public sealed class KableServerOptions
{
    internal KableServerOptions Snapshot()
    {
        if (MaxConnections <= 0) throw new ArgumentOutOfRangeException(nameof(MaxConnections));
        if (SessionQueueCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(SessionQueueCapacity));
        ValidateTimeout(DefaultBroadcastTimeout, nameof(DefaultBroadcastTimeout));
        ValidateTimeout(DefaultShutdownTimeout, nameof(DefaultShutdownTimeout));
        return new KableServerOptions
        {
            MaxConnections = MaxConnections, SessionQueueCapacity = SessionQueueCapacity,
            DefaultBroadcastTimeout = DefaultBroadcastTimeout, DefaultShutdownTimeout = DefaultShutdownTimeout
        };
    }

    internal static void ValidateTimeout(TimeSpan timeout, string name)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(name);
    }

    /// <summary>
    /// 최대 동시 접속 수 (기본값: 10,000)
    /// </summary>
    public int MaxConnections { get; set; } = 10_000;

    /// <summary>
    /// 세션별 송신 큐 용량 (기본값: 1,000)
    /// </summary>
    public int SessionQueueCapacity { get; set; } = 1_000;

    /// <summary>
    /// 기본 브로드캐스트 전체 타임아웃 (기본값: 1초)
    /// </summary>
    public TimeSpan DefaultBroadcastTimeout { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 정상 종료 드레인 타임아웃 (기본값: 3초)
    /// </summary>
    public TimeSpan DefaultShutdownTimeout { get; set; } = TimeSpan.FromSeconds(3);
}
