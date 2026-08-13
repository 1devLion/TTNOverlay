namespace TTNOverlay.Services;

/// <summary>
/// Portuguese UI strings, assembled from the per-section files in this folder (Common.cs,
/// ModerationPanel.cs, SettingsAlerts.cs, etc.), mirroring the same section names as En/. Any key
/// missing here falls back to English automatically (see Strings.Get).
/// </summary>
internal static partial class PtStrings
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