namespace TTNOverlay.Services;

internal static partial class DeStrings
{
    private static readonly Dictionary<string, string> SettingsStreamlabsEntries = new()
    {
        ["Settings_Streamlabs_Header"] = "Streamlabs",
        ["Settings_Streamlabs_Info"] = "Bringt Spenden, Follows, Hosts und Merch, die nicht über IRC kommen, ohne Twitch-OAuth.",
        ["Settings_Streamlabs_Enable"] = "Streamlabs-Ereignisse aktivieren",
        ["Settings_Streamlabs_SocketToken"] = "Socket-API-Token",
        ["Settings_Streamlabs_WidgetToken"] = "Widget-Token",
        ["Settings_Streamlabs_WidgetTokenInfo"] = "Wird verwendet, um die gespeicherte Text-/Bildkonfiguration der Alert Box abzurufen.",
        ["Settings_Streamlabs_SourceLabel"] = "Quelle für überlappende Alarme (Abo/Re-Abo/verschenktes Abo/Raid)",
        ["Settings_Streamlabs_SourceBoth"] = "Beide (Streamlabs bevorzugen)",
        ["Settings_Streamlabs_SourceIrcOnly"] = "Nur IRC",
        ["Settings_Streamlabs_SourceStreamlabsOnly"] = "Nur Streamlabs",
        ["Settings_Streamlabs_SourceInfo"] = "Bei \"Beide\" wird, wenn Streamlabs aktiviert ist, dessen Version für überlappende Ereignisse bevorzugt (es bringt Serie, benutzerdefiniertes Bild usw.); IRC wird nur für das verwendet, was Streamlabs nicht melden kann (Ritual, Ankündigungen, Bits-Abzeichen).",
    };
}