namespace Kable.UI.Wpf.Models;

public sealed class LanguageOption
{
    public string DisplayName { get; }
    public string CultureCode { get; }

    public LanguageOption(string displayName, string cultureCode)
    {
        DisplayName = displayName;
        CultureCode = cultureCode;
    }

    public override string ToString() => DisplayName;

    public static IReadOnlyList<LanguageOption> DefaultLanguages { get; } = new[]
    {
        new LanguageOption("🇰🇷 한국어 (KO)", "ko-KR"),
        new LanguageOption("🇺🇸 English (EN)", "en-US"),
        new LanguageOption("🇹🇼 繁體中文 (ZH-TW)", "zh-TW"),
        new LanguageOption("🇨🇳 简体中文 (ZH-CN)", "zh-CN"),
        new LanguageOption("🇯🇵 日本語 (JA)", "ja-JP"),
        new LanguageOption("🇩🇪 Deutsch (DE)", "de-DE"),
        new LanguageOption("🇪🇸 Español (ES)", "es-ES")
    };
}
