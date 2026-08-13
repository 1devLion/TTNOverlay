namespace TTNOverlay.Services;

internal static partial class PtStrings
{
    private static readonly Dictionary<string, string> ChatConnectionEntries = new()
    {
        ["MainWindow_ChannelConnected"] = "#{0} (conectado)",
        ["MainWindow_ErrorLabel"] = "Erro: {0}",
        ["MainWindow_Disconnected"] = "Desconectado: {0}",
        ["MainWindow_Connecting"] = "Conectando a #{0}...",
        ["MainWindow_FirstTime"] = "Sem canal configurado",
        ["MainWindow_ConnectFailedTitle"] = "Erro de conexão",
        ["MainWindow_ConnectFailedBody"] = "Não foi possível conectar ao canal '{0}':\n{1}\n\nLog completo em:\n{2}",
    };
}