namespace TTNOverlay.Services;

internal static partial class DeStrings
{
    private static readonly Dictionary<string, string> SettingsHotkeysEntries = new()
    {
        ["Settings_Hotkeys_Enable"] = "Globale Tastenkürzel aktivieren",
        ["Settings_Hotkeys_ToggleEvents"] = "Ereignisbereich ein-/ausblenden",
        ["Settings_Hotkeys_OpenModeration"] = "Moderationsbereich öffnen",
        ["Settings_Hotkeys_ToggleBorders"] = "Rahmen ein-/ausblenden",
        ["Settings_Hotkeys_Info"] = "Klicken Sie auf ein Feld und drücken Sie die gewünschte Kombination. Erfordert mindestens eine Modifikationstaste (Ctrl/Alt/Shift/Win). Drücken Sie Escape, um die Zuweisung aufzuheben.",
        ["Settings_Hotkey_Unassigned"] = "(nicht zugewiesen)",
        ["Settings_Hotkey_NeedsModifier"] = "Benötigt Ctrl, Alt, Shift oder Win",
        ["Settings_Hotkey_AlreadyTaken"] = "Bereits von einem anderen Tastenkürzel verwendet",
    };
}