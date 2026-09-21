namespace Kable.UI.Wpf.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using Kable.Observability;

public partial class PacketCatalogItem : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private TrafficKind _kind = TrafficKind.AperiodicCommand;

    [ObservableProperty]
    private string _commandPayload = string.Empty;

    [ObservableProperty]
    private string _responseTemplate = string.Empty;

    [ObservableProperty]
    private int _intervalMs = 100; // 상시(Periodic) 패킷일 때 갱신 주기 (ms)

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private double _simulatedBaseValue = 25.0;

    [ObservableProperty]
    private bool _isEnabled = true;

    public bool IsPeriodic => Kind == TrafficKind.PeriodicTelemetry;
    public bool IsAperiodic => Kind == TrafficKind.AperiodicCommand;
    public bool IsAlarm => Kind == TrafficKind.SpontaneousAlarm;

    partial void OnKindChanged(TrafficKind value)
    {
        OnPropertyChanged(nameof(IsPeriodic));
        OnPropertyChanged(nameof(IsAperiodic));
        OnPropertyChanged(nameof(IsAlarm));
    }
}
