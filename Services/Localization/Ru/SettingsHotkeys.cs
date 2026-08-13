namespace TTNOverlay.Services;

internal static partial class RuStrings
{
    private static readonly Dictionary<string, string> SettingsHotkeysEntries = new()
    {
        ["Settings_Hotkeys_Enable"] = "Включить глобальные горячие клавиши",
        ["Settings_Hotkeys_ToggleEvents"] = "Переключить панель событий",
        ["Settings_Hotkeys_OpenModeration"] = "Открыть панель модерации",
        ["Settings_Hotkeys_ToggleBorders"] = "Переключить границы",
        ["Settings_Hotkeys_Info"] = "Нажмите на поле и введите желаемую комбинацию. Требуется как минимум один модификатор (Ctrl/Alt/Shift/Win). Нажмите Escape, чтобы сбросить назначение.",
        ["Settings_Hotkey_Unassigned"] = "(не назначена)",
        ["Settings_Hotkey_NeedsModifier"] = "Требуется Ctrl, Alt, Shift или Win",
        ["Settings_Hotkey_AlreadyTaken"] = "Уже используется другой горячей клавишей",
    };
}