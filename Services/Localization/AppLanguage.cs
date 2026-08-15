namespace TTNOverlay.Services;

/// <summary>
/// UI languages looked up by <see cref="Strings"/> and tracked by <see cref="LocalizationService"/>.
/// </summary>
public enum AppLanguage
{
    English,
    Deutsch,
    French,
    日本語,
    Portuguese,
    Русский,
    Spanish,
    简体中文,

}

/// <summary>
/// Converts between the string stored in Settings.Language (which doubles as the dropdown label)
/// and the <see cref="AppLanguage"/> enum.
/// </summary>
public static class AppLanguageExtensions
{
    public static AppLanguage FromSettingsLabel(string? label) =>
        Enum.TryParse<AppLanguage>(label, out var lang) ? lang : AppLanguage.English;
}