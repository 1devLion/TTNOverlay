namespace TTNOverlay.Services;

internal static partial class ZhStrings
{
    private static readonly Dictionary<string, string> SettingsGeneralEntries = new()
    {
        ["Settings_Language"] = "语言",
        ["Settings_Language_English"] = "英语",
        ["Settings_Language_Spanish"] = "西班牙语",
        ["Settings_WindowTitle"] = "设置",
        ["Settings_Section_General"] = "常规",
        ["Settings_Section_Hotkeys"] = "热键",
        ["Settings_Section_TwitchApi"] = "Twitch API",
        ["Settings_Section_Streamlabs"] = "Streamlabs",
        ["Settings_Section_Alerts"] = "提醒",
        ["Settings_Section_Audio"] = "音频",
        ["Settings_Section_About"] = "关于",
        ["Settings_General_Theme"] = "主题",
        ["Settings_Theme_Dark"] = "暗色",
        ["Settings_Theme_Light"] = "亮色",
        ["Settings_General_Channel"] = "Twitch 频道",
        ["Settings_General_FontSize"] = "字体大小",
        ["Settings_General_MessageLifetime"] = "消息存活时间（秒）",
        ["Settings_General_MessageLifetimeInfo"] = "0 表示消息永不过期，始终保留在聊天中。不推荐：会显著增加资源占用。",
        ["Settings_General_MaxMessages"] = "屏幕最大消息数",
        ["Settings_General_ClickThrough"] = "点击穿透（鼠标点击穿透覆盖层）",
        ["Settings_General_DebugMode"] = "启用调试日志",
        ["Settings_General_ThirdPartyEmotes"] = "第三方表情（BTTV/FFZ/7TV）",
        ["Settings_General_EnableEventsPanel"] = "启用事件面板",
        ["Settings_General_EnableModerationPanel"] = "启用审核面板",
        ["Settings_General_HighQualityMedia"] = "高质量媒体",
        ["Settings_General_HighQualityMediaInfo"] = "以原始分辨率解码动画表情和提醒，而不是缩小它们。更清晰，但占用更多内存。",
    };
}