namespace TTNOverlay.Services;

internal static partial class JaStrings
{
    private static readonly Dictionary<string, string> AutoModMessagesEntries = new()
    {
        ["Moderation_TabAutoMod"] = "AutoMod",
        ["Moderation_TabAutoModCount"] = "AutoMod ({0})",
        ["Moderation_AutoModNeedLogin"] = "AutoModに保留されたメッセージを確認するには、モデレーターアカウントでログインしてください（チャッタータブ）。",
        ["Moderation_AutoModEmpty"] = "AutoModが保留しているメッセージはありません。",
        ["Moderation_AutoModConnecting"] = "AutoModに接続中...",
        ["Moderation_AutoModUnavailable"] = "AutoModの確認は利用できません。Twitchからログアウトして再ログインし、このアカウントがチャンネルのモデレーターであることを確認してください。",
        ["Moderation_AutoModAllow"] = "許可",
        ["Moderation_AutoModDeny"] = "拒否",
        ["Moderation_AutoModReasonGeneric"] = "AutoModにより保留",
        ["Moderation_AutoModReasonBlockedTerm"] = "保留：ブロックされた用語を含みます",
        ["Moderation_AutoModReasonCategory"] = "AutoModにより保留：{0}",
        ["Moderation_AutoModReasonCategoryLevel"] = "AutoModにより保留：{0}（レベル{1}）",
        ["Moderation_AutoModAllowed"] = "{0}のメッセージを許可しました。",
        ["Moderation_AutoModDenied"] = "{0}のメッセージを拒否しました。",
        ["Moderation_AutoModAlreadyResolved"] = "{0}のメッセージはすでに処理されています。",
        ["Moderation_AutoModFailed"] = "{0}のメッセージを更新できませんでした。",
        ["Moderation_AutoModNeedsRelogin"] = "AutoModのメッセージを確認するには、Twitchからログアウトして再ログインしてください（新しい権限が必要です）。",
        ["EventMsg_AutoModHeld"] = "AutoModが{0}のメッセージを保留しています。",
    };
}
