namespace TTNOverlay.Services;

internal static partial class EnStrings
{
    private static readonly Dictionary<string, string> SettingsTwitchApiEntries = new()
    {
        ["Settings_TwitchApi_Enable"] = "Enable Twitch API",
        ["Settings_TwitchApi_LoginInfo"] = "Log in with Twitch to enable the moderation panel, the viewer count widget, and badges.",
        ["Settings_TwitchApi_ShowViewerCount"] = "Show viewer count",
        ["Settings_TwitchApi_ViewerCountBackground"] = "Viewer count background",
        ["Settings_TwitchApi_ViewerCountTextColor"] = "Viewer count text color",
        ["Settings_TwitchApi_ViewerCountSize"] = "Viewer count size",
        ["Settings_TwitchApi_ResetViewerCountColor"] = "Reset theme color",
        ["Settings_TwitchApi_ShowBadges"] = "Show badges",
        ["Settings_TwitchApi_NotLoggedIn"] = "You are not logged in with Twitch.",
        ["Settings_TwitchApi_Connected"] = "Connected as {0}",
        ["Settings_TwitchApi_OpeningBrowser"] = "Opening the browser to log in...",
        ["Settings_TwitchApi_LoginFailed"] = "Could not log in. Try again.",
    };
}