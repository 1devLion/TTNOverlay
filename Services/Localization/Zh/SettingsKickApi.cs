namespace TTNOverlay.Services;

internal static partial class ZhStrings
{
    private static readonly Dictionary<string, string> SettingsKickApiEntries = new()
    {
        ["Settings_Section_KickApi"] = "Kick API",
        ["Settings_KickApi_LoginInfo"] = "使用 Kick 登录，即可在聊天中使用 Kick 的管理工具。",
        ["Settings_KickApi_Connected"] = "已连接为 {0}",
        ["Settings_KickApi_NotLoggedIn"] = "尚未使用 Kick 登录。",
        ["Settings_KickApi_OpeningBrowser"] = "正在打开浏览器进行登录...",
        ["Settings_KickApi_LoginFailed"] = "登录失败，请重试。",
        ["Common_LoginWithKick"] = "使用 Kick 登录",
        ["Kick_ScopesChangedTitle"] = "新的 Kick 权限",
        ["Kick_ScopesChangedMessage"] = "此更新新增了需要新 Kick 权限的功能，因此已退出你的 Kick 会话。请重新登录以启用。",
        ["Kick_DeleteNeedsRelogin"] = "要管理 Kick 聊天，请先退出 Kick 再重新登录（需要新的权限）。",
        ["Kick_MsgNeedLogin"] = "要管理 Kick 消息，请在 设置 > Kick API 中登录 Kick。",
        ["Moderation_TabSanctions"] = "处罚",
        ["Kick_PermissionMissing"] = "缺少 Kick 权限：{0}。请退出 Kick 并重新登录，在授权界面勾选该权限。",
        ["Kick_SanctionsNote"] = "Kick 没有提供被处罚用户列表：此处显示自打开此面板以来记录到的封禁和禁言。",
        ["Kick_SanctionsEmpty"] = "暂无处罚记录。",
        ["Auth_BrowserSuccess"] = "已完成，现在可以关闭此标签页。",
        ["Auth_BrowserFailed"] = "登录失败。可以关闭此标签页。",
        ["Auth_BrowserSuccessTitle"] = "登录成功",
    };
}
