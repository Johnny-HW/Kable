namespace Kable.Observability;

using System;
using System.Threading;
using Kable.Alarms;

/// <summary>
/// 통신 세션의 왕복 시간(RTT), 지터(Jitter) 및 패킷 손실률을 실시간으로 감시하는 워치독.
/// 통신 품질 저하 또는 이상 징후 감지 시 알람 매니저로 경고를 자동 통지합니다.
/// </summary>
public sealed class SessionHealthMonitor
{
    private readonly int _windowSize;
    private readonly double[] _rttWindow;
    private int _head;
    private int _count;
    private readonly object _lock = new();

    private long _totalPackets;
    private long _timeoutCount;
    private readonly IAlarmManager? _alarmManager;
    private readonly string _deviceId;
    private readonly double _jitterWarningThresholdMs;

    public SessionHealthMonitor(
        int windowSize = 50,
        double jitterWarningThresholdMs = 50.0,
        IAlarmManager? alarmManager = null,
        string deviceId = "DEFAULT")
    {
        if (windowSize <= 0) windowSize = 50;
        _windowSize = windowSize;
        _rttWindow = new double[windowSize];
        _jitterWarningThresholdMs = jitterWarningThresholdMs;
        _alarmManager = alarmManager;
        _deviceId = deviceId;
    }

    /// <summary>
    /// 커맨드 완료 시 소요된 RTT(밀리초)를 기록합니다.
    /// </summary>
    public void RecordRtt(double rttMs)
    {
        Interlocked.Increment(ref _totalPackets);

        lock (_lock)
        {
            _rttWindow[_head] = rttMs;
            _head = (_head + 1) % _windowSize;
            if (_count < _windowSize) _count++;
        }

        CheckJitterWarning();
    }

    /// <summary>
    /// 타임아웃 발생 시 기록합니다.
    /// </summary>
    public void RecordTimeout()
    {
        Interlocked.Increment(ref _totalPackets);
        Interlocked.Increment(ref _timeoutCount);
    }

    /// <summary>
    /// 현재 통신 품질 메트릭 스냅샷을 계산하여 반환합니다.
    /// </summary>
    public SessionHealth GetSnapshot()
    {
        double[] snapshot;
        int count;

        lock (_lock)
        {
            count = _count;
            if (count == 0)
            {
                return new SessionHealth(0, 0, 0, 0, _totalPackets, _timeoutCount, 0, 100);
            }

            snapshot = new double[count];
            Array.Copy(_rttWindow, snapshot, count);
        }

        double sum = 0;
        double min = double.MaxValue;
        double max = double.MinValue;

        for (int i = 0; i < count; i++)
        {
            double v = snapshot[i];
            sum += v;
            if (v < min) min = v;
            if (v > max) max = v;
        }

        double avg = sum / count;

        // 표준편차(Jitter) 계산
        double varianceSum = 0;
        for (int i = 0; i < count; i++)
        {
            double diff = snapshot[i] - avg;
            varianceSum += diff * diff;
        }
        double jitter = Math.Sqrt(varianceSum / count);

        long total = Volatile.Read(ref _totalPackets);
        long timeouts = Volatile.Read(ref _timeoutCount);
        double lossRate = total > 0 ? (double)timeouts / total : 0;

        // 건강 점수 산출 (100점 만점 기준: 타임아웃률 및 지터 페널티)
        int score = 100;
        score -= (int)(lossRate * 200); // 1% 손실 시 -2점
        if (jitter > 20) score -= (int)((jitter - 20) / 2);
        score = score < 0 ? 0 : (score > 100 ? 100 : score);

        return new SessionHealth(avg, min, max, jitter, total, timeouts, lossRate, score);
    }

    private void CheckJitterWarning()
    {
        if (_alarmManager == null) return;

        var snap = GetSnapshot();
        if (snap.JitterMs >= _jitterWarningThresholdMs)
        {
            _alarmManager.RaiseAlarm(
                alarmId: "ALM_HIGH_JITTER",
                deviceId: _deviceId,
                severity: AlarmSeverity.Warning,
                description: $"High communication jitter detected: {snap.JitterMs:F1}ms (Threshold: {_jitterWarningThresholdMs}ms)");
        }
    }
}
