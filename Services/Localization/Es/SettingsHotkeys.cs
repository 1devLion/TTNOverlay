namespace TTNOverlay.Services;

internal static partial class EsStrings
{
    private static readonly Dictionary<string, string> SettingsHotkeysEntries = new()
    {
        ["Settings_Hotkeys_Enable"] = "Activar atajos globales",
        ["Settings_Hotkeys_ToggleEvents"] = "Mostrar/ocultar panel de eventos",
        ["Settings_Hotkeys_OpenModeration"] = "Abrir panel de moderación",
        ["Settings_Hotkeys_ToggleBorders"] = "Mostrar/ocultar bordes",
        ["Settings_Hotkeys_Info"] = "Hacé clic en un campo y presioná la combinación deseada. Requiere al menos un modificador (Ctrl/Alt/Shift/Win). Presioná Escape para dejarlo sin asignar.",
        ["Settings_Hotkey_Unassigned"] = "(sin asignar)",
        ["Settings_Hotkey_NeedsModifier"] = "Necesita Ctrl, Alt, Shift o Win",
        ["Settings_Hotkey_AlreadyTaken"] = "Ya está en uso por otro atajo",
    };
}
