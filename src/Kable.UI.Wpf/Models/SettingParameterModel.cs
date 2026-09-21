namespace Kable.UI.Wpf.Models;

using CommunityToolkit.Mvvm.ComponentModel;

public partial class SettingParameterModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _commandPrefix = string.Empty;

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private double _minValue;

    [ObservableProperty]
    private double _maxValue;

    [ObservableProperty]
    private double _step = 1.0;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private string _lastAckMessage = "대기 중";

    public string BuildPayload() => $"{CommandPrefix}={Value:F1}";
}
