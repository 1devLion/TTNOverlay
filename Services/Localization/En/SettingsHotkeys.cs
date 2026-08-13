namespace TTNOverlay.Services;

internal static partial class EnStrings
{
    private static readonly Dictionary<string, string> SettingsHotkeysEntries = new()
    {
        ["Settings_Hotkeys_Enable"] = "Enable global hotkeys",
        ["Settings_Hotkeys_ToggleEvents"] = "Toggle events panel",
        ["Settings_Hotkeys_OpenModeration"] = "Open moderation panel",
        ["Settings_Hotkeys_ToggleBorders"] = "Toggle borders",
        ["Settings_Hotkeys_Info"] = "Click a field and press the desired combination. Requires at least one modifier (Ctrl/Alt/Shift/Win). Press Escape to unassign it.",
        ["Settings_Hotkey_Unassigned"] = "(unassigned)",
        ["Settings_Hotkey_NeedsModifier"] = "Needs Ctrl, Alt, Shift or Win",
        ["Settings_Hotkey_AlreadyTaken"] = "Already in use by another hotkey",
    };
}
