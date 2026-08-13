namespace TTNOverlay.Services;

internal static partial class ZhStrings
{
    private static readonly Dictionary<string, string> ChatConnectionEntries = new()
    {
        ["MainWindow_ChannelConnected"] = "#{0}（已连接）",
        ["MainWindow_ErrorLabel"] = "错误：{0}",
        ["MainWindow_Disconnected"] = "已断开：{0}",
        ["MainWindow_Connecting"] = "正在连接到 #{0}……",
        ["MainWindow_FirstTime"] = "未配置频道",
        ["MainWindow_ConnectFailedTitle"] = "连接错误",
        ["MainWindow_ConnectFailedBody"] = "无法连接到频道“{0}”：\n{1}\n\n完整日志位于：\n{2}",
    };
}