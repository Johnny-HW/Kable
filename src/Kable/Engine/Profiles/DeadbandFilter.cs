namespace Kable.Engine.Profiles;

using System;

/// <summary>
/// 미세한 아날로그 센서/파라미터 변동으로 인한 불필요한 상위 리포팅을 억제하는 데드밴드(Deadband) 필터
/// </summary>
public sealed class DeadbandFilter<T> where T : struct, IComparable<T>
{
    private readonly double _deadbandDelta;
    private double? _lastReportedValue;
    private readonly object _lock = new();

    public double Deadband => _deadbandDelta;
    public bool HasValue { get { lock (_lock) return _lastReportedValue.HasValue; } }
    public double? LastPublishedValue { get { lock (_lock) return _lastReportedValue; } }

    public DeadbandFilter(double deadbandDelta)
    {
        if (deadbandDelta < 0) throw new ArgumentOutOfRangeException(nameof(deadbandDelta), "Deadband must be non-negative.");
        _deadbandDelta = deadbandDelta;
    }

    /// <summary>
    /// 새로운 값이 들어왔을 때, 데드밴드 임계치를 초과하여 상위에 보고할 가치가 있는지 판별합니다.
    /// </summary>
    /// <param name="currentValue">현재 측정값</param>
    /// <param name="valAsDouble">변환된 측정값</param>
    /// <returns>임계치 초과 시 true, 억제 시 false</returns>
    public bool Evaluate(T currentValue, out double valAsDouble)
    {
        valAsDouble = Convert.ToDouble(currentValue);

        lock (_lock)
        {
            if (!_lastReportedValue.HasValue)
            {
                _lastReportedValue = valAsDouble;
                return true;
            }

            double delta = Math.Abs(valAsDouble - _lastReportedValue.Value);
            if (delta >= _deadbandDelta)
            {
                _lastReportedValue = valAsDouble;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// ShouldPublish 편의 메서드 (원래 타입 T 반환)
    /// </summary>
    public bool ShouldPublish(T currentValue, out T publishedValue)
    {
        publishedValue = currentValue;
        return Evaluate(currentValue, out _);
    }

    /// <summary>
    /// 필터 상태 초기화
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _lastReportedValue = null;
        }
    }
}
