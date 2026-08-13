namespace TTNOverlay.Services;

/// <summary>
/// UI languages looked up by <see cref="Strings"/> and tracked by <see cref="LocalizationService"/>.
///
/// To add a language: add its enum value here (the name doubles as the value persisted in
/// Settings.Language and the label shown in the dropdown -- see AppLanguageExtensions.FromSettingsLabel),
/// then add a Services/Localization/&lt;Code&gt;/ folder with the translated tables. Existing keys don't
/// need to move or be touched -- untranslated ones simply fall back to English (see Strings.Get).
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
/// and the <see cref="AppLanguage"/> enum. Central spot so Program.cs, RevertGeneral(), etc. don't
/// each need their own English/Spanish-only special case.
/// </summary>
public static class AppLanguageExtensions
{
    public static AppLanguage FromSettingsLabel(string? label) =>
        Enum.TryParse<AppLanguage>(label, out var lang) ? lang : AppLanguage.English;
}