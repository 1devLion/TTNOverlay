namespace TTNOverlay.Services;

internal static partial class EnStrings
{
    private static readonly Dictionary<string, string> ChatConnectionEntries = new()
    {
        ["MainWindow_ChannelConnected"] = "#{0} (connected)",
        ["MainWindow_ErrorLabel"] = "Error: {0}",
        ["MainWindow_Disconnected"] = "Disconnected: {0}",
        ["MainWindow_Connecting"] = "Connecting to #{0}...",
        ["MainWindow_FirstTime"] = "No channel configured",
        ["MainWindow_ConnectFailedTitle"] = "Connection error",
        ["MainWindow_ConnectFailedBody"] = "Could not connect to channel '{0}':\n{1}\n\nFull log at:\n{2}",
    };
}
