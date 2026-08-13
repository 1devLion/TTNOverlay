namespace TTNOverlay.Services;

/// <summary>
/// English UI strings, assembled from the per-section files in this folder (Common.cs,
/// ModerationPanel.cs, SettingsAlerts.cs, etc.). English is the fallback language every other
/// language's missing keys resolve to (see Strings.Get), so this table is expected to always be
/// complete.
/// </summary>
internal static partial class EnStrings
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
    };
}
