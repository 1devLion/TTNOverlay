namespace TTNOverlay.Services;

internal static partial class FrStrings
{
    private static readonly Dictionary<string, string> SettingsHotkeysEntries = new()
    {
        ["Settings_Hotkeys_Enable"] = "Activer les raccourcis globaux",
        ["Settings_Hotkeys_ToggleEvents"] = "Basculer le panneau d'événements",
        ["Settings_Hotkeys_OpenModeration"] = "Ouvrir le panneau de modération",
        ["Settings_Hotkeys_ToggleBorders"] = "Basculer les bordures",
        ["Settings_Hotkeys_Info"] = "Cliquez sur un champ et appuyez sur la combinaison souhaitée. Nécessite au moins un modificateur (Ctrl/Alt/Shift/Win). Appuyez sur Échap pour le désaffecter.",
        ["Settings_Hotkey_Unassigned"] = "(non assigné)",
        ["Settings_Hotkey_NeedsModifier"] = "Nécessite Ctrl, Alt, Shift ou Win",
        ["Settings_Hotkey_AlreadyTaken"] = "Déjà utilisé par un autre raccourci",
    };
}