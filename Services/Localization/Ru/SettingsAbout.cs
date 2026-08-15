namespace TTNOverlay.Services;

internal static partial class RuStrings
{
    private static readonly Dictionary<string, string> SettingsAboutEntries = new()
    {
        ["Settings_About_SupportUs"] = "Поддержите нас ❤️",
        ["Settings_About_Author"] = "Автор: ",
        ["Settings_About_License"] = "Лицензия: ",
        ["Settings_About_LicenseText"] = "Открытое программное обеспечение. Вы можете свободно использовать, копировать, изменять и распространять эту программу при условии сохранения оригинального уведомления об авторских правах.",
        ["Settings_About_VersionFormat"] = "Версия {0} ({1}-бит)",
        ["Settings_About_DebugMode"] = "Режим отладки",
        ["Settings_About_DebugModeWarning"] = "Включение режима отладки влияет на производительность (требуется перезапуск). Включайте его только для диагностики конкретной проблемы, а затем отключайте. Файл журнала сохраняется в %appdata%\\TTNOverlay\\debug.log.",
    };
}