namespace TTNOverlay.Services;

internal static partial class DeStrings
{
    private static readonly Dictionary<string, string> SettingsTwitchApiEntries = new()
    {
        ["Settings_TwitchApi_Enable"] = "Twitch-API aktivieren",
        ["Settings_TwitchApi_LoginInfo"] = "Melden Sie sich mit Twitch an, um den Moderationsbereich, das Zuschauerzahlen-Widget und Abzeichen zu aktivieren.",
        ["Settings_TwitchApi_ShowViewerCount"] = "Zuschauerzahl anzeigen",
        ["Settings_TwitchApi_ViewerCountBackground"] = "Zuschauerzahl-Hintergrund",
        ["Settings_TwitchApi_ViewerCountSize"] = "Zuschauerzahl-Größe",
        ["Settings_TwitchApi_ResetViewerCountColor"] = "Design-Farbe zurücksetzen",
        ["Settings_TwitchApi_ShowBadges"] = "Abzeichen anzeigen",
        ["Settings_TwitchApi_NotLoggedIn"] = "Sie sind nicht mit Twitch angemeldet.",
        ["Settings_TwitchApi_Connected"] = "Angemeldet als {0}",
        ["Settings_TwitchApi_OpeningBrowser"] = "Öffne Browser zur Anmeldung...",
        ["Settings_TwitchApi_LoginFailed"] = "Anmeldung fehlgeschlagen. Versuchen Sie es erneut.",
    };
}