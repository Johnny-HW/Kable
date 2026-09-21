namespace Kable.UI.Wpf.Localization;

using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using Kable.Localization;

/// <summary>
/// 런타임 언어 변경 시 XAML의 Header 및 텍스트를 실시간으로 갱신하는 반응형 바인딩 마크업 확장입니다.
/// 사용법: Header="{loc:Loc Col_Seq}" 또는 Text="{loc:Loc Header_CommandConsole}"
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; }

    public LocExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(TranslationManager.CurrentCulture))
        {
            Source = TranslationManager.Instance,
            Converter = new LocKeyConverter(Key),
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}

/// <summary>
/// 런타임 다국어 변경 이벤트를 감지하여 바인딩을 리프레시하는 싱글톤 매니저입니다.
/// </summary>
public sealed class TranslationManager : INotifyPropertyChanged
{
    public static TranslationManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo CurrentCulture => KableLocalizer.Instance.CurrentCulture;

    private TranslationManager()
    {
        KableLocalizer.Instance.CultureChanged += (s, culture) =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
        };
    }

    public void ChangeCulture(CultureInfo culture)
    {
        KableLocalizer.Instance.SetCulture(culture);
    }
}

/// <summary>
/// CultureInfo 변경 신호를 받아 KableLocalizer에서 키에 맞는 번역 문자열을 반환하는 컨버터
/// </summary>
public sealed class LocKeyConverter : IValueConverter
{
    private readonly string _key;

    public LocKeyConverter(string key)
    {
        _key = key;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return KableLocalizer.Instance.GetString(_key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
