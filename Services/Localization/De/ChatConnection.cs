namespace TTNOverlay.Services;

internal static partial class DeStrings
{
    private static readonly Dictionary<string, string> ChatConnectionEntries = new()
    {
        ["MainWindow_ChannelConnected"] = "#{0} (verbunden)",
        ["MainWindow_ErrorLabel"] = "Fehler: {0}",
        ["MainWindow_Disconnected"] = "Getrennt: {0}",
        ["MainWindow_Connecting"] = "Verbinde mit #{0}...",
        ["MainWindow_FirstTime"] = "Kein Kanal konfiguriert",
        ["MainWindow_ConnectFailedTitle"] = "Verbindungsfehler",
        ["MainWindow_ConnectFailedBody"] = "Konnte keine Verbindung zum Kanal '{0}' herstellen:\n{1}\n\nVollständiges Protokoll unter:\n{2}",
    };
}