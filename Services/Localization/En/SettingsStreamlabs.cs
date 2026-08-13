namespace TTNOverlay.Services;

internal static partial class EnStrings
{
    private static readonly Dictionary<string, string> SettingsStreamlabsEntries = new()
    {
        ["Settings_Streamlabs_Header"] = "Streamlabs",
        ["Settings_Streamlabs_Info"] = "Brings donations, follows, hosts and merch that don't come through IRC, without needing Twitch OAuth.",
        ["Settings_Streamlabs_Enable"] = "Enable Streamlabs events",
        ["Settings_Streamlabs_SocketToken"] = "Socket API token",
        ["Settings_Streamlabs_WidgetToken"] = "Widget token",
        ["Settings_Streamlabs_WidgetTokenInfo"] = "Used to fetch the Alert Box's saved text/image configuration.",
        ["Settings_Streamlabs_SourceLabel"] = "Source for overlapping alerts (sub/resub/subgift/raid)",
        ["Settings_Streamlabs_SourceBoth"] = "Both (prefer Streamlabs)",
        ["Settings_Streamlabs_SourceIrcOnly"] = "IRC only",
        ["Settings_Streamlabs_SourceStreamlabsOnly"] = "Streamlabs only",
        ["Settings_Streamlabs_SourceInfo"] = "In \"Both\", if Streamlabs is enabled its version is preferred for overlapping events (it brings streak, custom image, etc.); IRC is only used for what Streamlabs can't report (ritual, announcements, bits badge).",
    };
}
