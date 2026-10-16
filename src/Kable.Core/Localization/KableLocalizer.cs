namespace Kable.Localization;

using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Kable 기본 다국어 로컬라이저 구현체 (영어, 한국어, 중국어 간체/번체, 일본어, 독일어, 프랑스어 지원)
/// </summary>
public sealed partial class KableLocalizer : IKableLocalizer
{
    public static KableLocalizer Instance { get; } = new();

    private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("en-US");

    public CultureInfo CurrentCulture => _currentCulture;

    public event EventHandler<CultureInfo>? CultureChanged;

    private readonly Dictionary<string, Dictionary<KableErrorCode, string>> _errorDictionary = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _stringDictionary = new(StringComparer.OrdinalIgnoreCase);

    public KableLocalizer()
    {
        InitializeDefaultTranslations();
    }

    public void SetCulture(CultureInfo culture)
    {
        if (culture == null) throw new ArgumentNullException(nameof(culture));
        if (!Equals(_currentCulture, culture))
        {
            _currentCulture = culture;
            CultureChanged?.Invoke(this, culture);
        }
    }

    public string GetErrorMessage(KableErrorCode code, params object[] args)
    {
        string cultureKey = GetMatchingCultureKey(_currentCulture);
        if (_errorDictionary.TryGetValue(cultureKey, out var langDict) && langDict.TryGetValue(code, out var template))
        {
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        // Fallback to English
        if (_errorDictionary.TryGetValue("en", out var enDict) && enDict.TryGetValue(code, out var enTemplate))
        {
            return args.Length > 0 ? string.Format(enTemplate, args) : enTemplate;
        }

        return $"[Error {code}]";
    }

    public string GetString(string key, params object[] args)
    {
        string cultureKey = GetMatchingCultureKey(_currentCulture);
        if (_stringDictionary.TryGetValue(cultureKey, out var langDict) && langDict.TryGetValue(key, out var template))
        {
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        if (_stringDictionary.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enTemplate))
        {
            return args.Length > 0 ? string.Format(enTemplate, args) : enTemplate;
        }

        return key;
    }

    public void RegisterCustomError(string cultureCode, KableErrorCode code, string messageTemplate)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) throw new ArgumentNullException(nameof(cultureCode));
        if (string.IsNullOrWhiteSpace(messageTemplate)) throw new ArgumentNullException(nameof(messageTemplate));

        if (!_errorDictionary.TryGetValue(cultureCode, out var dict))
        {
            dict = new Dictionary<KableErrorCode, string>();
            _errorDictionary[cultureCode] = dict;
        }
        dict[code] = messageTemplate;
    }

    public void RegisterCustomString(string cultureCode, string key, string valueTemplate)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) throw new ArgumentNullException(nameof(cultureCode));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
        if (string.IsNullOrWhiteSpace(valueTemplate)) throw new ArgumentNullException(nameof(valueTemplate));

        if (!_stringDictionary.TryGetValue(cultureCode, out var dict))
        {
            dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _stringDictionary[cultureCode] = dict;
        }
        dict[key] = valueTemplate;
    }

    private string GetMatchingCultureKey(CultureInfo culture)
    {
        string name = culture.Name;
        if (name.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return "ko";
        if (string.Equals(name, "zh-TW", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(name, "zh-HK", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "zh-Hant", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-TW";
        }
        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (name.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja";
        if (name.StartsWith("de", StringComparison.OrdinalIgnoreCase)) return "de";
        if (name.StartsWith("fr", StringComparison.OrdinalIgnoreCase)) return "fr";
        if (name.StartsWith("es", StringComparison.OrdinalIgnoreCase)) return "es";
        return "en";
    }

    private void InitializeDefaultTranslations()
    {
        InitializeEnglishTranslations();
        InitializeKoreanTranslations();
        InitializeChineseSimplifiedTranslations();
        InitializeChineseTraditionalTranslations();
        InitializeJapaneseTranslations();
        InitializeGermanTranslations();
        InitializeFrenchTranslations();
        InitializeSpanishTranslations();
    }
}
