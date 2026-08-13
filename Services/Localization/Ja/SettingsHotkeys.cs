namespace TTNOverlay.Services;

internal static partial class JaStrings
{
    private static readonly Dictionary<string, string> SettingsHotkeysEntries = new()
    {
        ["Settings_Hotkeys_Enable"] = "グローバルホットキーを有効化",
        ["Settings_Hotkeys_ToggleEvents"] = "イベントパネルの切り替え",
        ["Settings_Hotkeys_OpenModeration"] = "モデレーションパネルを開く",
        ["Settings_Hotkeys_ToggleBorders"] = "枠線の切り替え",
        ["Settings_Hotkeys_Info"] = "フィールドをクリックし、希望するキーコンビネーションを押してください。Ctrl/Alt/Shift/Win のいずれかの修飾キーが必須です。Escキーを押すと割り当てを解除します。",
        ["Settings_Hotkey_Unassigned"] = "(未割り当て)",
        ["Settings_Hotkey_NeedsModifier"] = "Ctrl、Alt、Shift、Win のいずれかが必要です",
        ["Settings_Hotkey_AlreadyTaken"] = "他のホットキーですでに使用されています",
    };
}