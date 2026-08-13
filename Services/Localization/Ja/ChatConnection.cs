namespace TTNOverlay.Services;

internal static partial class JaStrings
{
    private static readonly Dictionary<string, string> ChatConnectionEntries = new()
    {
        ["MainWindow_ChannelConnected"] = "#{0} (接続済み)",
        ["MainWindow_ErrorLabel"] = "エラー: {0}",
        ["MainWindow_Disconnected"] = "切断: {0}",
        ["MainWindow_Connecting"] = "#{0} に接続中...",
        ["MainWindow_FirstTime"] = "チャンネル未設定",
        ["MainWindow_ConnectFailedTitle"] = "接続エラー",
        ["MainWindow_ConnectFailedBody"] = "チャンネル '{0}' に接続できませんでした:\n{1}\n\n完全なログは:\n{2}",
    };
}