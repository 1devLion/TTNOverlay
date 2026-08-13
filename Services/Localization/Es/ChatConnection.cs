namespace TTNOverlay.Services;

internal static partial class EsStrings
{
    private static readonly Dictionary<string, string> ChatConnectionEntries = new()
    {
        ["MainWindow_ChannelConnected"] = "#{0} (conectado)",
        ["MainWindow_ErrorLabel"] = "Error: {0}",
        ["MainWindow_Disconnected"] = "Desconectado: {0}",
        ["MainWindow_Connecting"] = "Conectando a #{0}...",
        ["MainWindow_FirstTime"] = "Sin canal configurado",
        ["MainWindow_ConnectFailedTitle"] = "Error de conexión",
        ["MainWindow_ConnectFailedBody"] = "No se pudo conectar al canal '{0}':\n{1}\n\nLog completo en:\n{2}",
    };
}
