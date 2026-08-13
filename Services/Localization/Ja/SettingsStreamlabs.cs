namespace TTNOverlay.Services;

internal static partial class JaStrings
{
    private static readonly Dictionary<string, string> SettingsStreamlabsEntries = new()
    {
        ["Settings_Streamlabs_Header"] = "Streamlabs",
        ["Settings_Streamlabs_Info"] = "IRC経由でない寄付、フォロー、ホスト、マーチを、Twitch OAuthなしで取得します。",
        ["Settings_Streamlabs_Enable"] = "Streamlabsイベントを有効化",
        ["Settings_Streamlabs_SocketToken"] = "Socket APIトークン",
        ["Settings_Streamlabs_WidgetToken"] = "ウィジェットトークン",
        ["Settings_Streamlabs_WidgetTokenInfo"] = "アラートボックスの保存されたテキスト/画像設定を取得するために使用します。",
        ["Settings_Streamlabs_SourceLabel"] = "重複イベント (サブ/再サブ/ギフトサブ/レイド) のソース",
        ["Settings_Streamlabs_SourceBoth"] = "両方 (Streamlabs優先)",
        ["Settings_Streamlabs_SourceIrcOnly"] = "IRCのみ",
        ["Settings_Streamlabs_SourceStreamlabsOnly"] = "Streamlabsのみ",
        ["Settings_Streamlabs_SourceInfo"] = "「両方」の場合、Streamlabsが有効であれば、重複イベントではStreamlabs版が優先されます (ストリーク、カスタム画像など)。IRCはStreamlabsが報告できないイベント (儀式、アナウンス、ビッツバッジ) にのみ使用されます。",
    };
}