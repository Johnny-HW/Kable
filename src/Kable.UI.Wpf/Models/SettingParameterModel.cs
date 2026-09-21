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

    /// <summary>
    /// Kable.Core의 순수 CommandDefinition 불변 구조체로부터 파라미터 제어 모델 생성
    /// </summary>
    public static SettingParameterModel FromDefinition(Kable.Protocol.CommandDefinition def)
    {
        return new SettingParameterModel
        {
            Id = def.Id,
            DisplayName = string.IsNullOrWhiteSpace(def.Name) ? def.Id : def.Name,
            CommandPrefix = string.IsNullOrWhiteSpace(def.RequestPayload) ? $"SET {def.Id}" : def.RequestPayload,
            Value = def.DefaultValue,
            MinValue = def.MinValue,
            MaxValue = def.MaxValue,
            Step = def.Step > 0 ? def.Step : 1.0,
            Unit = def.Unit,
            ScaleFactor = def.ScaleFactor > 0 ? def.ScaleFactor : 1.0,
            LastAckMessage = "Ready"
        };
    }

    /// <summary>
    /// 현재 파라미터 모델을 순수 CommandDefinition 불변 구조체로 추출
    /// </summary>
    public Kable.Protocol.CommandDefinition ToDefinition()
    {
        return new Kable.Protocol.CommandDefinition(
            Id,
            DisplayName,
            CommandPrefix,
            "",
            Unit,
            Value,
            MinValue,
            MaxValue,
            Step,
            ScaleFactor
        );
    }
}
