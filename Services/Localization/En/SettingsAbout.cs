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
        ["Settings_About_DebugModeWarning"] = "Enabling debug mode affects performance (requires restart). Only turn it on to diagnose a specific issue, then turn it back off. The log file is saved to %appdata%\\TTNOverlay\\debug.log.",
    };
}