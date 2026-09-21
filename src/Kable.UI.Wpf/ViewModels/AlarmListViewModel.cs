namespace Kable.UI.Wpf.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kable.Core;
using Kable.Observability;

public sealed class AlarmDisplayModel : ObservableObject
{
    public string AlarmCode { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string SeverityColor { get; set; } = "#F38BA8"; // Red
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string State { get; set; } = "Active";
}

public partial class AlarmListViewModel : ObservableObject, ICommObserver
{
    private readonly Dispatcher _dispatcher;

    [ObservableProperty]
    private string _filterText = string.Empty;

    public ObservableCollection<AlarmDisplayModel> Alarms { get; } = new();

    public ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();

    public AlarmListViewModel(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
    }

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        // 알람 통신 필터링
        if (trace.Kind != TrafficKind.SpontaneousAlarm) return;

        string alarmCode = !string.IsNullOrEmpty(trace.Tag) ? trace.Tag : "ALM_COMM_EVT";
        string severity = trace.Level >= LogLevel.Critical ? "CRITICAL" :
                          trace.Level >= LogLevel.Error ? "ERROR" : "WARNING";
        string color = severity == "CRITICAL" ? "#F38BA8" :
                       severity == "ERROR" ? "#FAB387" : "#F9E2AF";

        var model = new AlarmDisplayModel
        {
            AlarmCode = alarmCode,
            Severity = severity,
            SeverityColor = color,
            Message = trace.ParsedText ?? $"Alarm event triggered on {trace.DeviceId}",
            Timestamp = trace.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff"),
            DeviceId = trace.DeviceId,
            State = "ACTIVE"
        };

        _dispatcher.BeginInvoke(() =>
        {
            if (Alarms.Count >= 500)
            {
                Alarms.RemoveAt(Alarms.Count - 1);
            }
            Alarms.Insert(0, model);
        });
    }

    [RelayCommand]
    public void ClearAlarms()
    {
        Alarms.Clear();
    }
}
