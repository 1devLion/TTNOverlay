namespace TTNOverlay.Services;

internal static partial class EnStrings
{
    private static readonly Dictionary<string, string> SettingsGeneralEntries = new()
    {
        ["Settings_Language"] = "Language",
        ["Settings_Language_English"] = "English",
        ["Settings_Language_Spanish"] = "Spanish",
        ["Settings_WindowTitle"] = "Settings",
        ["Settings_Section_General"] = "General",
        ["Settings_Section_Hotkeys"] = "Hotkeys",
        ["Settings_Section_TwitchApi"] = "Twitch API",
        ["Settings_Section_Streamlabs"] = "Streamlabs",
        ["Settings_Section_Alerts"] = "Alerts",
        ["Settings_Section_Audio"] = "Audio",
        ["Settings_Section_About"] = "About",
        ["Settings_General_Theme"] = "Theme",
        ["Settings_Theme_Dark"] = "Dark",
        ["Settings_Theme_Light"] = "Light",
        ["Settings_General_Channel"] = "Twitch channel",
        ["Settings_General_FontSize"] = "Font size",
        ["Settings_General_MessageLifetime"] = "Message lifetime (seconds)",
        ["Settings_General_MessageLifetimeInfo"] = "0 = messages never expire and stay in the chat. Not recommended: uses significantly more resources.",
        ["Settings_General_MaxMessages"] = "Max. messages on screen",
        ["Settings_General_ClickThrough"] = "Click-through (mouse clicks pass through the overlay)",
        ["Settings_General_DebugMode"] = "Enable debug log",
        ["Settings_General_ThirdPartyEmotes"] = "Third-party emotes (BTTV/FFZ/7TV)",
        ["Settings_General_EnableEventsPanel"] = "Enable events panel",
        ["Settings_General_EnableModerationPanel"] = "Enable moderation panel",
        ["Settings_General_HighQualityMedia"] = "High-quality media",
        ["Settings_General_HighQualityMediaInfo"] = "Decodes animated emotes and alerts at their native resolution instead of downscaling them. Sharper, but uses more RAM.",
    };
}
