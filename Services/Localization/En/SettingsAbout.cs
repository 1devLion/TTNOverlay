namespace TTNOverlay.Services;

internal static partial class EnStrings
{
    private static readonly Dictionary<string, string> SettingsAboutEntries = new()
    {
        ["Settings_About_SupportUs"] = "Support us ❤️",
        ["Settings_About_Author"] = "Author: ",
        ["Settings_About_License"] = "License: ",
        ["Settings_About_LicenseText"] = "Open-source software. You can freely use, copy, modify and distribute this program, as long as the original copyright notice is kept.",
        ["Settings_About_VersionFormat"] = "Version {0} ({1}-bit)",
        ["Settings_About_DebugMode"] = "Debug mode",
        ["Settings_About_DebugModeWarning"] = "Enabling debug mode affects performance (requires restart). Only turn it on to diagnose a specific issue, then turn it back off.",
        ["Settings_About_DebugLogLocation"] = "Log file",
        ["Settings_About_GoTo"] = "Go to",
        ["Settings_About_CheckUpdates"] = "Check for updates",
        ["Settings_About_Checking"] = "Checking...",
        ["Settings_About_UpToDate"] = "You're up to date.",
        ["Settings_About_UpdateAvailable"] = "Version {0} is available.",
        ["Settings_About_UpdateNow"] = "Update now",
        ["Settings_About_UpdateFailed"] = "Couldn't check for updates.",
        ["Settings_About_UpdateNotInstalled"] = "Updates are only available in the installed version.",
        ["Settings_Restart_Title"] = "Restart required",
        ["Settings_Restart_Message"] = "Changing debug mode requires restarting the app. Do you want to restart now?",
        ["Settings_Restart_Now"] = "Restart now",
        ["Settings_Restart_Later"] = "Later",
    };
}