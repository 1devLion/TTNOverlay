namespace TTNOverlay.Services;

internal static partial class ZhStrings
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