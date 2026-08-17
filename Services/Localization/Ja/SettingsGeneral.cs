namespace TTNOverlay.Services;

internal static partial class JaStrings
{
    private static readonly Dictionary<string, string> SettingsGeneralEntries = new()
    {
        ["Settings_Language"] = "言語",
        ["Settings_Language_English"] = "英語",
        ["Settings_Language_Spanish"] = "スペイン語",
        ["Settings_WindowTitle"] = "設定",
        ["Settings_Section_General"] = "一般",
        ["Settings_Section_Hotkeys"] = "ホットキー",
        ["Settings_Section_TwitchApi"] = "Twitch API",
        ["Settings_Section_Streamlabs"] = "Streamlabs",
        ["Settings_Section_Alerts"] = "アラート",
        ["Settings_Section_Audio"] = "オーディオ",
        ["Settings_Section_About"] = "バージョン情報",
        ["Settings_Section_ViewerCount"] = "視聴者数",
        ["Settings_General_Theme"] = "テーマ",
        ["Settings_Theme_Dark"] = "ダーク",
        ["Settings_Theme_Light"] = "ライト",
        ["Settings_General_Channel"] = "Twitchチャンネル",
        ["Settings_General_ChatSource"] = "チャットソース",
        ["Settings_ChatSource_Twitch"] = "Twitch",
        ["Settings_ChatSource_Kick"] = "Kick",
        ["Settings_ChatSource_Multichat"] = "マルチチャット（Twitch + Kick）",
        ["Settings_General_ChannelKick"] = "Kickチャンネル",
        ["Settings_General_ChannelShared"] = "チャンネル",
        ["Settings_General_MultichatEnableTwitch"] = "Twitchを有効化",
        ["Settings_General_MultichatEnableKick"] = "Kickを有効化",
        ["Settings_General_MultichatUseSameChannel"] = "両方に同じチャンネル名を使用",
        ["Settings_General_FontSize"] = "フォントサイズ",
        ["Settings_General_MessageLifetime"] = "メッセージの寿命 (秒)",
        ["Settings_General_MessageLifetimeInfo"] = "0 = メッセージは期限切れにならず、チャットに残り続けます。非推奨：リソース消費が大幅に増加します。",
        ["Settings_General_MaxMessages"] = "画面に表示する最大メッセージ数",
        ["Settings_General_ClickThrough"] = "クリックスルー (マウスクリックがオーバーレイを透過)",
        ["Settings_General_DebugMode"] = "デバッグログを有効化",
        ["Settings_General_ThirdPartyEmotes"] = "サードパーティ絵文字 (BTTV/FFZ/7TV)",
        ["Settings_General_EnableEventsPanel"] = "イベントパネルを有効化",
        ["Settings_General_EnableModerationPanel"] = "モデレーションパネルを有効化",
        ["Settings_General_HighQualityMedia"] = "高品質メディア",
        ["Settings_General_HighQualityMediaInfo"] = "アニメーション絵文字やアラートを縮小せずに本来の解像度でデコードします。より鮮明ですが、RAMを多く使用します。",
    };
}