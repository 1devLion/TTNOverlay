namespace TTNOverlay.Services;

internal static partial class ZhStrings
{
    private static readonly Dictionary<string, string> AutoModMessagesEntries = new()
    {
        ["Moderation_TabAutoMod"] = "AutoMod",
        ["Moderation_TabAutoModCount"] = "AutoMod ({0})",
        ["Moderation_AutoModNeedLogin"] = "请使用管理员账号登录（聊天用户标签页）以审核被 AutoMod 拦截的消息。",
        ["Moderation_AutoModEmpty"] = "AutoMod 当前没有拦截任何消息。",
        ["Moderation_AutoModConnecting"] = "正在连接 AutoMod...",
        ["Moderation_AutoModUnavailable"] = "无法使用 AutoMod 审核。请退出并重新登录 Twitch，并确认此账号是该频道的管理员。",
        ["Moderation_AutoModAllow"] = "允许",
        ["Moderation_AutoModDeny"] = "拒绝",
        ["Moderation_AutoModReasonGeneric"] = "已被 AutoMod 拦截",
        ["Moderation_AutoModReasonBlockedTerm"] = "已拦截：包含屏蔽词",
        ["Moderation_AutoModReasonCategory"] = "已被 AutoMod 拦截：{0}",
        ["Moderation_AutoModReasonCategoryLevel"] = "已被 AutoMod 拦截：{0}（级别 {1}）",
        ["Moderation_AutoModAllowed"] = "已允许 {0} 的消息。",
        ["Moderation_AutoModDenied"] = "已拒绝 {0} 的消息。",
        ["Moderation_AutoModAlreadyResolved"] = "{0} 的消息已被处理。",
        ["Moderation_AutoModFailed"] = "无法更新 {0} 的消息。",
        ["Moderation_AutoModNeedsRelogin"] = "要审核 AutoMod 消息，请退出并重新登录 Twitch（需要新的权限）。",
        ["EventMsg_AutoModHeld"] = "AutoMod 拦截了 {0} 的一条消息。",
    };
}
