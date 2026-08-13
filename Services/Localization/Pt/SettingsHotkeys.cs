namespace TTNOverlay.Services;

internal static partial class PtStrings
{
    private static readonly Dictionary<string, string> SettingsHotkeysEntries = new()
    {
        ["Settings_Hotkeys_Enable"] = "Ativar atalhos globais",
        ["Settings_Hotkeys_ToggleEvents"] = "Mostrar/ocultar painel de eventos",
        ["Settings_Hotkeys_OpenModeration"] = "Abrir painel de moderação",
        ["Settings_Hotkeys_ToggleBorders"] = "Mostrar/ocultar bordas",
        ["Settings_Hotkeys_Info"] = "Clique em um campo e pressione a combinação desejada. Requer pelo menos um modificador (Ctrl/Alt/Shift/Win). Pressione Escape para deixar sem atribuição.",
        ["Settings_Hotkey_Unassigned"] = "(sem atribuição)",
        ["Settings_Hotkey_NeedsModifier"] = "Precisa de Ctrl, Alt, Shift ou Win",
        ["Settings_Hotkey_AlreadyTaken"] = "Já está em uso por outro atalho",
    };
}