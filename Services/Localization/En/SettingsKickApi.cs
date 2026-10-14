namespace TTNOverlay.Services;

internal static partial class EnStrings
{
    private static readonly Dictionary<string, string> SettingsKickApiEntries = new()
    {
        ["Settings_Section_KickApi"] = "Kick API",
        ["Settings_KickApi_LoginInfo"] = "Log in with Kick to use the Kick moderation tools in your chat.",
        ["Settings_KickApi_Connected"] = "Connected as {0}",
        ["Settings_KickApi_NotLoggedIn"] = "You're not logged in with Kick.",
        ["Settings_KickApi_OpeningBrowser"] = "Opening the browser to log in...",
        ["Settings_KickApi_LoginFailed"] = "Login failed. Try again.",
        ["Common_LoginWithKick"] = "Log in with Kick",
        ["Kick_ScopesChangedTitle"] = "New Kick permissions",
        ["Kick_ScopesChangedMessage"] = "This update adds features that need new Kick permissions, so your Kick session was closed. Log in again to enable them.",
        ["Kick_DeleteNeedsRelogin"] = "To moderate Kick chat, log out and log in with Kick again (new permissions are required).",
        ["Kick_MsgNeedLogin"] = "To moderate Kick messages, log in with Kick in the Stream tab (or Settings > Kick API).",
        ["Moderation_TabSanctions"] = "Sanctions",
        ["Kick_PermissionMissing"] = "Kick permission missing: {0}. Log out and log in to Kick again, ticking it on the consent screen.",
        ["Kick_SanctionsNote"] = "Kick has no list of banned users: this shows the bans and timeouts seen since this panel was opened.",
        ["Kick_SanctionsEmpty"] = "No sanctions recorded.",
        ["Auth_BrowserSuccess"] = "All set, you can close this tab now.",
        ["Auth_BrowserFailed"] = "Login failed. You can close this tab.",
    };
}
