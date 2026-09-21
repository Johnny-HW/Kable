namespace Kable.Alarms;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Channels;

/// <summary>
/// Kable 표준 알람 라이프사이클 관리자 구현체
/// </summary>
public sealed class AlarmManager : IAlarmManager
{
    private readonly ConcurrentDictionary<string, AlarmRecord> _activeAlarms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<AlarmRecord> _channel;

    public IReadOnlyCollection<AlarmRecord> ActiveAlarms => (IReadOnlyCollection<AlarmRecord>)_activeAlarms.Values;
    public ChannelReader<AlarmRecord> AlarmStream => _channel.Reader;

    public AlarmManager(int bufferCapacity = 1000)
    {
        var options = new BoundedChannelOptions(bufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<AlarmRecord>(options);
    }

    private static string BuildKey(string deviceId, string alarmId) => $"{deviceId}::{alarmId}";

    public void RaiseAlarm(string alarmId, string deviceId, AlarmSeverity severity, string description)
    {
        var record = new AlarmRecord(alarmId, deviceId, AlarmState.Set, severity, description);
        string key = BuildKey(deviceId, alarmId);

        _activeAlarms[key] = record;
        _channel.Writer.TryWrite(record);
    }

    public bool ClearAlarm(string alarmId, string deviceId, string? clearReason = null)
    {
        string key = BuildKey(deviceId, alarmId);
        if (_activeAlarms.TryRemove(key, out var active))
        {
            var clearRecord = new AlarmRecord(
                alarmId, 
                deviceId, 
                AlarmState.Clear, 
                active.Severity, 
                clearReason ?? $"Cleared: {active.Description}");

            _channel.Writer.TryWrite(clearRecord);
            return true;
        }
        return false;
    }

    public void ClearAll(string? deviceId = null)
    {
        foreach (var kvp in _activeAlarms)
        {
            if (deviceId == null || string.Equals(kvp.Value.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                ClearAlarm(kvp.Value.AlarmId, kvp.Value.DeviceId, "Batch cleared");
            }
        }
    }
}
