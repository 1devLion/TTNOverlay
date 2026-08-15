namespace TTNOverlay.Services;

/// <summary>
/// Russian UI strings, assembled from the per-section files in this folder (Common.ru.cs,
/// ModerationPanel.ru.cs, SettingsAlerts.ru.cs, etc.). This table is intended to be complete
/// for all keys used in the application; missing keys fall back to English (see Strings.Get).
/// </summary>
internal static partial class RuStrings
{
    private static Dictionary<string, string>? _map;
    public static Dictionary<string, string> Map => _map ??= BuildMap();

    private static Dictionary<string, string> BuildMap()
    {
        var map = new Dictionary<string, string>();
        foreach (var section in Sections)
        {
            foreach (var (key, value) in section)
                map[key] = value;
        }
        return map;
    }

    private static IEnumerable<Dictionary<string, string>> Sections => new[]
    {
        CommonEntries,
        ChatConnectionEntries,
        ModerationPanelEntries,
        ModerationMessagesEntries,
        TrayEntries,
        SettingsGeneralEntries,
        SettingsHotkeysEntries,
        SettingsTwitchApiEntries,
        SettingsStreamlabsEntries,
        SettingsAlertsEntries,
        SettingsAudioEntries,
        SettingsAboutEntries,
        EventTypesEntries,
        EventMessagesEntries,
        StreamlabsMessagesEntries,
        UpdateEntries,
    };
}