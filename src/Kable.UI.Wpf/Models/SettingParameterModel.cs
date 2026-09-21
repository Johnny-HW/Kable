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
    private double _scaleFactor = 10.0; // e.g. 0.1 deg unit -> x10

    [ObservableProperty]
    private string _lastAckMessage = "Ready";

    // Auto-converted Raw integer value (Register / DAC Count)
    public int RawValue => (int)Math.Round(Value * ScaleFactor);

    // 자동 환산된 16진수 Hex 포맷 (예: 0x01C2)
    public string HexRawValue => $"0x{RawValue:X4}";

    // 자동 환산된 전송 프로토콜 페이로드 미리보기
    public string ConvertedPayloadPreview => $"{CommandPrefix}={Value:F1} (Raw: {HexRawValue} / {RawValue})";

    partial void OnValueChanged(double value)
    {
        OnPropertyChanged(nameof(RawValue));
        OnPropertyChanged(nameof(HexRawValue));
        OnPropertyChanged(nameof(ConvertedPayloadPreview));
    }

    public string BuildPayload() => $"{CommandPrefix}={Value:F1} RAW={HexRawValue}";
}
