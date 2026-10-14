namespace TTNOverlay.Services;

internal static partial class DeStrings
{
    private static readonly Dictionary<string, string> SettingsKickApiEntries = new()
    {
        ["Settings_Section_KickApi"] = "Kick-API",
        ["Settings_KickApi_LoginInfo"] = "Melde dich bei Kick an, um die Kick-Moderationswerkzeuge in deinem Chat zu nutzen.",
        ["Settings_KickApi_Connected"] = "Angemeldet als {0}",
        ["Settings_KickApi_NotLoggedIn"] = "Du bist nicht bei Kick angemeldet.",
        ["Settings_KickApi_OpeningBrowser"] = "Browser wird zur Anmeldung geöffnet...",
        ["Settings_KickApi_LoginFailed"] = "Anmeldung fehlgeschlagen. Versuche es erneut.",
        ["Common_LoginWithKick"] = "Mit Kick anmelden",
        ["Kick_ScopesChangedTitle"] = "Neue Kick-Berechtigungen",
        ["Kick_ScopesChangedMessage"] = "Dieses Update fügt Funktionen hinzu, die neue Kick-Berechtigungen benötigen, daher wurde deine Kick-Sitzung beendet. Melde dich erneut an, um sie zu aktivieren.",
        ["Kick_DeleteNeedsRelogin"] = "Um den Kick-Chat zu moderieren, melde dich bei Kick ab und wieder an (neue Berechtigungen sind erforderlich).",
        ["Kick_MsgNeedLogin"] = "Um Kick-Nachrichten zu moderieren, melde dich in den Einstellungen unter Kick-API bei Kick an.",
        ["Moderation_TabSanctions"] = "Sanktionen",
        ["Kick_PermissionMissing"] = "Kick-Berechtigung fehlt: {0}. Melde dich bei Kick ab und wieder an und hake sie auf dem Berechtigungsbildschirm an.",
        ["Kick_SanctionsNote"] = "Kick bietet keine Liste gesperrter Nutzer an: Hier stehen die Sperren und Timeouts, die seit dem Öffnen dieses Panels erfasst wurden.",
        ["Kick_SanctionsEmpty"] = "Keine Sanktionen erfasst.",
        ["Auth_BrowserSuccess"] = "Fertig, du kannst diesen Tab jetzt schließen.",
        ["Auth_BrowserFailed"] = "Anmeldung fehlgeschlagen. Du kannst diesen Tab schließen.",
    };
}
