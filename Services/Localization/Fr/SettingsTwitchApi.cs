namespace TTNOverlay.Services;

internal static partial class FrStrings
{
    private static readonly Dictionary<string, string> SettingsTwitchApiEntries = new()
    {
        ["Settings_TwitchApi_Enable"] = "Activer l'API Twitch",
        ["Settings_TwitchApi_LoginInfo"] = "Connectez-vous avec Twitch pour activer le panneau de modération, le widget de compteur de spectateurs et les badges.",
        ["Settings_TwitchApi_ShowViewerCount"] = "Afficher le nombre de spectateurs",
        ["Settings_TwitchApi_ViewerCountTextColor"] = "Couleur du texte du compteur de spectateurs",
        ["Settings_TwitchApi_ShowBadges"] = "Afficher les badges",
        ["Settings_TwitchApi_NotLoggedIn"] = "Vous n'êtes pas connecté avec Twitch.",
        ["Settings_TwitchApi_Connected"] = "Connecté en tant que {0}",
        ["Settings_TwitchApi_OpeningBrowser"] = "Ouverture du navigateur pour se connecter...",
        ["Settings_TwitchApi_LoginFailed"] = "Échec de la connexion. Réessayez.",
    };
}