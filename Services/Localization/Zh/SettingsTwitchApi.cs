namespace TTNOverlay.Services;

internal static partial class ZhStrings
{
    private static readonly Dictionary<string, string> SettingsTwitchApiEntries = new()
    {
        ["Settings_TwitchApi_Enable"] = "启用 Twitch API",
        ["Settings_TwitchApi_LoginInfo"] = "使用 Twitch 登录以启用审核面板、观众数小部件和徽章。",
        ["Settings_TwitchApi_ShowViewerCount"] = "显示观众数",
        ["Settings_TwitchApi_ViewerCountMode"] = "观众数显示方式",
        ["Settings_ViewerCountMode_Sum"] = "总计（所有平台之和）",
        ["Settings_ViewerCountMode_PerPlatform"] = "自定义",
        ["Settings_TwitchApi_ViewerCountIncludeTwitch"] = "包含 Twitch",
        ["Settings_TwitchApi_ViewerCountIncludeKick"] = "包含 Kick",
        ["Settings_TwitchApi_ViewerCountIncludeYouTube"] = "包含 YouTube",
        ["Settings_TwitchApi_ViewerCountBackground"] = "观众数背景",
        ["Settings_TwitchApi_ViewerCountTextColor"] = "观众数文字颜色",
        ["Settings_TwitchApi_ViewerCountSize"] = "观众数大小",
        ["Settings_TwitchApi_ResetViewerCountColor"] = "重置为主题颜色",
        ["Settings_TwitchApi_ShowBadges"] = "显示徽章",
        ["Settings_TwitchApi_NotLoggedIn"] = "您尚未登录 Twitch。",
        ["Settings_TwitchApi_Connected"] = "已连接为 {0}",
        ["Settings_TwitchApi_OpeningBrowser"] = "正在打开浏览器登录……",
        ["Settings_TwitchApi_LoginFailed"] = "登录失败，请重试。",
    };
}