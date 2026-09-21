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

    [ObservableProperty]
    private string _currentSettingValue = string.Empty;

    public bool IsPeriodic => Kind == TrafficKind.PeriodicTelemetry;
    public bool IsAperiodic => Kind == TrafficKind.AperiodicCommand;
    public bool IsAlarm => Kind == TrafficKind.SpontaneousAlarm;

    partial void OnKindChanged(TrafficKind value)
    {
        OnPropertyChanged(nameof(IsPeriodic));
        OnPropertyChanged(nameof(IsAperiodic));
        OnPropertyChanged(nameof(IsAlarm));
    }

    /// <summary>
    /// Kable.Core의 순수 불변 구조체(ScheduledCommandItem)로 변환
    /// </summary>
    public Kable.Protocol.ScheduledCommandItem ToScheduledItem()
    {
        var def = new Kable.Protocol.CommandDefinition(
            Id,
            Name,
            CommandPayload,
            ResponseTemplate,
            Unit,
            SimulatedBaseValue
        );

        var mode = Kind switch
        {
            TrafficKind.PeriodicTelemetry => Kable.Protocol.CommandExecutionMode.Periodic,
            _ => Kable.Protocol.CommandExecutionMode.Aperiodic
        };

        return new Kable.Protocol.ScheduledCommandItem(def, mode, IntervalMs, IsEnabled);
    }

    /// <summary>
    /// Kable.Core의 순수 불변 구조체로부터 WPF 바인딩 아이템 생성
    /// </summary>
    public static PacketCatalogItem FromScheduledItem(Kable.Protocol.ScheduledCommandItem item)
    {
        return new PacketCatalogItem
        {
            Id = item.Definition.Id,
            Name = item.Definition.Name,
            Kind = item.Mode == Kable.Protocol.CommandExecutionMode.Periodic 
                ? TrafficKind.PeriodicTelemetry 
                : TrafficKind.AperiodicCommand,
            CommandPayload = item.Definition.RequestPayload,
            ResponseTemplate = item.Definition.ResponsePayload,
            IntervalMs = item.IntervalMs,
            Unit = item.Definition.Unit,
            SimulatedBaseValue = item.Definition.DefaultValue,
            IsEnabled = item.IsEnabled
        };
    }
}
