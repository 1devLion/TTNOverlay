namespace TTNOverlay.Services;

internal static partial class DeStrings
{
    private static readonly Dictionary<string, string> SettingsGeneralEntries = new()
    {
        ["Settings_Language"] = "Sprache",
        ["Settings_Language_English"] = "Englisch",
        ["Settings_Language_Spanish"] = "Spanisch",
        ["Settings_WindowTitle"] = "Einstellungen",
        ["Settings_Section_General"] = "Allgemein",
        ["Settings_Section_Hotkeys"] = "Tastenkürzel",
        ["Settings_Section_TwitchApi"] = "Twitch-API",
        ["Settings_Section_Streamlabs"] = "Streamlabs",
        ["Settings_Section_Alerts"] = "Alarme",
        ["Settings_Section_Audio"] = "Audio",
        ["Settings_Section_About"] = "Über",
        ["Settings_General_Theme"] = "Design",
        ["Settings_Theme_Dark"] = "Dunkel",
        ["Settings_Theme_Light"] = "Hell",
        ["Settings_General_Channel"] = "Twitch-Kanal",
        ["Settings_General_FontSize"] = "Schriftgröße",
        ["Settings_General_MessageLifetime"] = "Nachrichtenlebensdauer (Sekunden)",
        ["Settings_General_MessageLifetimeInfo"] = "0 = Nachrichten laufen nie ab und bleiben im Chat. Nicht empfohlen: deutlich höherer Ressourcenverbrauch.",
        ["Settings_General_MaxMessages"] = "Max. Nachrichten auf dem Bildschirm",
        ["Settings_General_ClickThrough"] = "Durchklickbar (Mausklicks gehen durch das Overlay)",
        ["Settings_General_DebugMode"] = "Debug-Protokoll aktivieren",
        ["Settings_General_ThirdPartyEmotes"] = "Emojis von Drittanbietern (BTTV/FFZ/7TV)",
        ["Settings_General_EnableEventsPanel"] = "Ereignisbereich aktivieren",
        ["Settings_General_EnableModerationPanel"] = "Moderationsbereich aktivieren",
        ["Settings_General_HighQualityMedia"] = "Hochwertige Medien",
        ["Settings_General_HighQualityMediaInfo"] = "Dekodiert animierte Emojis und Alarme in ihrer nativen Auflösung anstatt sie zu verkleinern. Schärfer, aber verbraucht mehr RAM.",
    };
}