namespace Kable.UI.Wpf.ViewModels;

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Data;
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

public partial class TelemetryStreamViewModel : ObservableObject, ICommObserver, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ConcurrentDictionary<string, TelemetryItemModel> _itemsMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _knownUnits = new(StringComparer.OrdinalIgnoreCase);
    private readonly ICollectionView _telemetryView;
    private long _clearGeneration;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _filterText = string.Empty;

    public ObservableCollection<TelemetryItemModel> TelemetryItems { get; } = new();

    public ICollectionView TelemetryView => _telemetryView;

    public Controls.RawPacketLogStreamViewModel RawLogStream { get; }

    public ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();

    public TelemetryStreamViewModel(Dispatcher? dispatcher = null, int maxLogCount = 1000)
    {
        _dispatcher = dispatcher ?? (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        RawLogStream = new Controls.RawPacketLogStreamViewModel(_dispatcher, maxLogCount);
        _telemetryView = CollectionViewSource.GetDefaultView(TelemetryItems);
        _telemetryView.Filter = MatchesFilter;
    }

    public void RegisterUnit(string parameterName, string unit)
    {
        if (!string.IsNullOrWhiteSpace(parameterName) && !string.IsNullOrWhiteSpace(unit))
        {
            _knownUnits[parameterName.Trim()] = unit.Trim();
        }
    }

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        OnPacketTraceCore(in trace, forwardToRawLog: true);
    }

    internal string OnPacketTraceCore(in PacketTraceRecord trace, bool forwardToRawLog)
    {
        if (trace.Kind != TrafficKind.PeriodicTelemetry) return string.Empty;
        if (IsPaused) return string.Empty;

        if (forwardToRawLog)
        {
            RawLogStream.OnPacketTrace(in trace);
        }

        string paramName = !string.IsNullOrEmpty(trace.Tag) ? trace.Tag : trace.DeviceId;
        string valueStr = trace.ParsedText ?? $"{trace.RawBytes.Length} bytes";
        string unit = ResolveUnit(paramName, valueStr, trace.RawBytes);
        var now = DateTime.UtcNow;
        string formattedTime = now.ToLocalTime().ToString("HH:mm:ss.fff");
        long generation = Volatile.Read(ref _clearGeneration);

        if (_itemsMap.TryGetValue(paramName, out var existing))
        {
            long count = Interlocked.Increment(ref existing.UpdateCount);
            var elapsed = (now - existing.LastReceivedUtc).TotalSeconds;
            double hz = elapsed > 0.001 ? Math.Round(1.0 / elapsed, 1) : existing.FrequencyHz;
            existing.LastReceivedUtc = now;

            DispatchToUi(() =>
            {
                if (Volatile.Read(ref _clearGeneration) != generation) return;
                existing.FrequencyHz = hz;
                existing.Value = valueStr;
                if (!string.IsNullOrEmpty(unit)) existing.Unit = unit;
                existing.LastUpdated = formattedTime;
                if (!string.IsNullOrWhiteSpace(FilterText)) _telemetryView.Refresh();
            });
            _ = count;
        }
        else
        {
            var newItem = new TelemetryItemModel
            {
                ParameterName = paramName,
                Value = valueStr,
                Unit = string.IsNullOrEmpty(unit) ? "-" : unit,
                FrequencyHz = 0,
                LastUpdated = formattedTime,
                LastReceivedUtc = now,
                UpdateCount = 1
            };

            if (_itemsMap.TryAdd(paramName, newItem))
            {
                DispatchToUi(() =>
                {
                    if (Volatile.Read(ref _clearGeneration) != generation) return;
                    if (_itemsMap.TryGetValue(paramName, out var current) && ReferenceEquals(current, newItem))
                    {
                        TelemetryItems.Add(newItem);
                    }
                });
            }
        }

        return unit;
    }

    partial void OnFilterTextChanged(string value)
    {
        DispatchToUi(() => _telemetryView.Refresh());
    }

    private bool MatchesFilter(object obj)
    {
        string query = FilterText?.Trim() ?? string.Empty;
        if (query.Length == 0) return true;
        if (obj is not TelemetryItemModel item) return false;

        return item.ParameterName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Value.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Unit.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveUnit(string paramName, string valueStr, ReadOnlyMemory<byte> rawBytes)
    {
        if (_knownUnits.TryGetValue(paramName, out var registered) && !string.IsNullOrEmpty(registered))
        {
            return registered;
        }

        if (!rawBytes.IsEmpty && !string.IsNullOrWhiteSpace(valueStr))
        {
            string rawText = Encoding.UTF8.GetString(rawBytes.Span).Trim();
            int idx = rawText.LastIndexOf(valueStr, StringComparison.Ordinal);
            if (idx >= 0)
            {
                string trailing = rawText[(idx + valueStr.Length)..].Trim();
                if (trailing.Length > 0 && trailing.Length <= 16)
                {
                    return trailing;
                }
            }
        }

        return string.Empty;
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
    public void ClearTelemetry()
    {
        Interlocked.Increment(ref _clearGeneration);
        _itemsMap.Clear();
        DispatchToUi(() => TelemetryItems.Clear());
    }

    [RelayCommand]
    public void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    public void Dispose()
    {
        RawLogStream.Dispose();
    }
}
