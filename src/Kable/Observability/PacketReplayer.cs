namespace Kable.Observability;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

/// <summary>
/// 캡처된 패킷 레코드 목록을 원래 타임스탬프 간격(Delta)에 맞춰 시뮬레이터 또는 파이프라인으로 재현(Replay)하는 엔진.
/// 현장 간헐적 결함 재현 시 배속(SpeedMultiplier) 제어를 지원합니다.
/// </summary>
public sealed class PacketReplayer
{
    private readonly IReadOnlyList<PacketTraceRecord> _records;
    private double _speedMultiplier = 1.0;

    public PacketReplayer(IReadOnlyList<PacketTraceRecord> records)
    {
        _records = records ?? throw new ArgumentNullException(nameof(records));
    }

    /// <summary>
    /// 재생 배속 설정 (예: 1.0=실시간, 2.0=2배속, 0=지연 없이 즉시 재생)
    /// </summary>
    public PacketReplayer WithSpeed(double speedMultiplier)
    {
        if (speedMultiplier < 0) throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        _speedMultiplier = speedMultiplier;
        return this;
    }

    /// <summary>
    /// 패킷 목록을 타임스탬프 델타에 맞춰 콜백 또는 파이프로 방출합니다.
    /// </summary>
    public async Task ReplayAsync(Func<PacketTraceRecord, Task> onPacket, CancellationToken ct = default)
    {
        if (_records.Count == 0) return;

        DateTime? prevTime = null;

        for (int i = 0; i < _records.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var record = _records[i];

            if (prevTime.HasValue && _speedMultiplier > 0)
            {
                var delta = record.TimestampUtc - prevTime.Value;
                if (delta > TimeSpan.Zero)
                {
                    var adjustedMs = delta.TotalMilliseconds / _speedMultiplier;
                    if (adjustedMs > 1)
                    {
                        await Task.Delay((int)adjustedMs, ct);
                    }
                }
            }

            prevTime = record.TimestampUtc;
            await onPacket(record);
        }
    }
}
