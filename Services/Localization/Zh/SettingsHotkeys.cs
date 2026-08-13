namespace TTNOverlay.Services;

internal static partial class ZhStrings
{
    private static readonly Dictionary<string, string> SettingsHotkeysEntries = new()
    {
        ["Settings_Hotkeys_Enable"] = "启用全局热键",
        ["Settings_Hotkeys_ToggleEvents"] = "切换事件面板",
        ["Settings_Hotkeys_OpenModeration"] = "打开审核面板",
        ["Settings_Hotkeys_ToggleBorders"] = "切换边框",
        ["Settings_Hotkeys_Info"] = "单击输入框并按下所需组合键。至少需要一个修饰键（Ctrl/Alt/Shift/Win）。按 Escape 可取消分配。",
        ["Settings_Hotkey_Unassigned"] = "（未分配）",
        ["Settings_Hotkey_NeedsModifier"] = "需要 Ctrl、Alt、Shift 或 Win",
        ["Settings_Hotkey_AlreadyTaken"] = "已被另一个热键使用",
    };
}