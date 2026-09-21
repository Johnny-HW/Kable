namespace Kable.UI.Wpf.ViewModels;

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kable.Core;
using Kable.Observability;

public sealed class TelemetryItemModel : ObservableObject
{
    public string ParameterName { get; set; } = string.Empty;

    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    private string _unit = string.Empty;
    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    private double _frequencyHz;
    public double FrequencyHz
    {
        get => _frequencyHz;
        set => SetProperty(ref _frequencyHz, value);
    }

    private string _lastUpdated = string.Empty;
    public string LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }

    internal long UpdateCount;
    internal DateTime LastReceivedUtc;
}

public partial class TelemetryStreamViewModel : ObservableObject, ICommObserver
{
    private readonly Dispatcher _dispatcher;
    private readonly ConcurrentDictionary<string, TelemetryItemModel> _itemsMap = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _filterText = string.Empty;

    public ObservableCollection<TelemetryItemModel> TelemetryItems { get; } = new();

    public Controls.RawPacketLogStreamViewModel RawLogStream { get; }

    public ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();

    public TelemetryStreamViewModel(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        RawLogStream = new Controls.RawPacketLogStreamViewModel(_dispatcher, 1000);
    }

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        // 상시 통신 (텔레메트리 스트림) 필터링
        if (trace.Kind != TrafficKind.PeriodicTelemetry) return;
        if (IsPaused) return;

        // 원본 패킷 스트림 로그로 전달
        RawLogStream.OnPacketTrace(in trace);

        string paramName = !string.IsNullOrEmpty(trace.Tag) ? trace.Tag : trace.DeviceId;
        string valueStr = trace.ParsedText ?? $"{trace.RawBytes.Length} bytes";
        var now = DateTime.UtcNow;

        if (_itemsMap.TryGetValue(paramName, out var existing))
        {
            existing.UpdateCount++;
            var elapsed = (now - existing.LastReceivedUtc).TotalSeconds;
            if (elapsed > 0.001)
            {
                existing.FrequencyHz = Math.Round(1.0 / elapsed, 1);
            }
            existing.LastReceivedUtc = now;
            existing.Value = valueStr;
            existing.LastUpdated = now.ToLocalTime().ToString("HH:mm:ss.fff");
        }
        else
        {
            var newItem = new TelemetryItemModel
            {
                ParameterName = paramName,
                Value = valueStr,
                Unit = "-",
                FrequencyHz = 0,
                LastUpdated = now.ToLocalTime().ToString("HH:mm:ss.fff"),
                LastReceivedUtc = now,
                UpdateCount = 1
            };
            if (_itemsMap.TryAdd(paramName, newItem))
            {
                _dispatcher.BeginInvoke(() => TelemetryItems.Add(newItem));
            }
        }
    }

    [RelayCommand]
    public void ClearTelemetry()
    {
        _itemsMap.Clear();
        TelemetryItems.Clear();
    }

    [RelayCommand]
    public void TogglePause()
    {
        IsPaused = !IsPaused;
    }
}
