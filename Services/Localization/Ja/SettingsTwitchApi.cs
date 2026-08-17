namespace TTNOverlay.Services;

internal static partial class JaStrings
{
    private static readonly Dictionary<string, string> SettingsTwitchApiEntries = new()
    {
        ["Settings_TwitchApi_Enable"] = "Twitch APIを有効化",
        ["Settings_TwitchApi_LoginInfo"] = "Twitchでログインすると、モデレーションパネル、視聴者数ウィジェット、バッジが利用可能になります。",
        ["Settings_TwitchApi_ShowViewerCount"] = "視聴者数を表示",
        ["Settings_TwitchApi_ViewerCountMode"] = "視聴者数の表示形式",
        ["Settings_ViewerCountMode_Sum"] = "合計（全プラットフォームの合計）",
        ["Settings_ViewerCountMode_PerPlatform"] = "カスタム",
        ["Settings_TwitchApi_ViewerCountIncludeTwitch"] = "Twitchを含める",
        ["Settings_TwitchApi_ViewerCountIncludeKick"] = "Kickを含める",
        ["Settings_TwitchApi_ViewerCountIncludeYouTube"] = "YouTubeを含める",
        ["Settings_TwitchApi_ViewerCountBackground"] = "視聴者数の背景",
        ["Settings_TwitchApi_ViewerCountTextColor"] = "視聴者数の文字色",
        ["Settings_TwitchApi_ViewerCountSize"] = "視聴者数のサイズ",
        ["Settings_TwitchApi_ResetViewerCountColor"] = "テーマの色にリセット",
        ["Settings_TwitchApi_ShowBadges"] = "バッジを表示",
        ["Settings_TwitchApi_NotLoggedIn"] = "Twitchにログインしていません。",
        ["Settings_TwitchApi_Connected"] = "{0} として接続中",
        ["Settings_TwitchApi_OpeningBrowser"] = "ブラウザを開いてログイン中...",
        ["Settings_TwitchApi_LoginFailed"] = "ログインできませんでした。再試行してください。",
    };
}