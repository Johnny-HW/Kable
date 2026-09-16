namespace Kable.Mqtt.Telemetry;

using System;
using System.Collections.Generic;

/// <summary>
/// 설비 및 센서 텔레메트리 계측 데이터 모델
/// </summary>
public sealed class TelemetryMetric
{
    public string Name { get; }
    public double Value { get; }
    public long TimestampTicks { get; }
    public IReadOnlyDictionary<string, string>? Tags { get; }

    public TelemetryMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null, long timestampTicks = 0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;
        Tags = tags;
        TimestampTicks = timestampTicks == 0 ? DateTime.UtcNow.Ticks : timestampTicks;
    }
}
