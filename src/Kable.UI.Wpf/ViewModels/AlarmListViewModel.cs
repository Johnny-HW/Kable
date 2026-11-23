namespace Kable.UI.Wpf.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Data;
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
    private readonly ICollectionView _alarmsView;
    private long _clearGeneration;

    [ObservableProperty]
    private string _filterText = string.Empty;

    public ObservableCollection<AlarmDisplayModel> Alarms { get; } = new();

    public ICollectionView AlarmsView => _alarmsView;

    public ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();

    public AlarmListViewModel(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        _alarmsView = CollectionViewSource.GetDefaultView(Alarms);
        _alarmsView.Filter = MatchesFilter;
    }

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
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

        long generation = Volatile.Read(ref _clearGeneration);
        DispatchToUi(() =>
        {
            if (Volatile.Read(ref _clearGeneration) != generation) return;
            if (Alarms.Count >= 500)
            {
                Alarms.RemoveAt(Alarms.Count - 1);
            }
            Alarms.Insert(0, model);
        });
    }

    partial void OnFilterTextChanged(string value)
    {
        DispatchToUi(() => _alarmsView.Refresh());
    }

    private bool MatchesFilter(object obj)
    {
        string query = FilterText?.Trim() ?? string.Empty;
        if (query.Length == 0) return true;
        if (obj is not AlarmDisplayModel alarm) return false;

        return alarm.AlarmCode.Contains(query, StringComparison.OrdinalIgnoreCase)
            || alarm.Severity.Contains(query, StringComparison.OrdinalIgnoreCase)
            || alarm.Message.Contains(query, StringComparison.OrdinalIgnoreCase)
            || alarm.DeviceId.Contains(query, StringComparison.OrdinalIgnoreCase)
            || alarm.State.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void DispatchToUi(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(action);
        }
    }

    [RelayCommand]
    public void ClearAlarms()
    {
        Interlocked.Increment(ref _clearGeneration);
        DispatchToUi(() => Alarms.Clear());
    }
}
