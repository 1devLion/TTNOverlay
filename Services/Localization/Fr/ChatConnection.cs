namespace TTNOverlay.Services;

internal static partial class FrStrings
{
    private static readonly Dictionary<string, string> ChatConnectionEntries = new()
    {
        ["MainWindow_ChannelConnected"] = "#{0} (connecté)",
        ["MainWindow_ErrorLabel"] = "Erreur : {0}",
        ["MainWindow_Disconnected"] = "Déconnecté : {0}",
        ["MainWindow_Connecting"] = "Connexion à #{0}...",
        ["MainWindow_FirstTime"] = "Aucune chaîne configurée",
        ["MainWindow_ConnectFailedTitle"] = "Erreur de connexion",
        ["MainWindow_ConnectFailedBody"] = "Impossible de se connecter à la chaîne '{0}' :\n{1}\n\nJournal complet à :\n{2}",
    };
}