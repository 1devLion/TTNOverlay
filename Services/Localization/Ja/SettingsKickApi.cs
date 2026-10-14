namespace TTNOverlay.Services;

internal static partial class JaStrings
{
    private static readonly Dictionary<string, string> SettingsKickApiEntries = new()
    {
        ["Settings_Section_KickApi"] = "Kick API",
        ["Settings_KickApi_LoginInfo"] = "Kickでログインすると、チャットでKickのモデレーションツールを使用できます。",
        ["Settings_KickApi_Connected"] = "{0} としてログイン中",
        ["Settings_KickApi_NotLoggedIn"] = "Kickにログインしていません。",
        ["Settings_KickApi_OpeningBrowser"] = "ブラウザを開いてログインしています...",
        ["Settings_KickApi_LoginFailed"] = "ログインに失敗しました。もう一度お試しください。",
        ["Common_LoginWithKick"] = "Kickでログイン",
        ["Kick_ScopesChangedTitle"] = "Kickの新しい権限",
        ["Kick_ScopesChangedMessage"] = "このアップデートでは新しいKickの権限が必要な機能が追加されたため、Kickのセッションを終了しました。もう一度ログインして有効にしてください。",
        ["Kick_DeleteNeedsRelogin"] = "Kickチャットをモデレートするには、Kickからログアウトして再度ログインしてください（新しい権限が必要です）。",
        ["Kick_MsgNeedLogin"] = "Kickのメッセージをモデレートするには、設定 > Kick API でKickにログインしてください。",
        ["Moderation_TabSanctions"] = "処分",
        ["Kick_PermissionMissing"] = "Kickの権限「{0}」がありません。Kickからログアウトして再度ログインし、権限の画面でチェックを入れてください。",
        ["Kick_SanctionsNote"] = "Kickには処分中ユーザーの一覧がないため、このパネルを開いてから確認できたBANとタイムアウトを表示しています。",
        ["Kick_SanctionsEmpty"] = "記録された処分はありません。",
        ["Auth_BrowserSuccess"] = "完了しました。このタブは閉じて大丈夫です。",
        ["Auth_BrowserFailed"] = "ログインに失敗しました。このタブは閉じて大丈夫です。",
    };
}
