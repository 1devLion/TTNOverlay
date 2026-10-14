namespace TTNOverlay.Services;

internal static partial class FrStrings
{
    private static readonly Dictionary<string, string> SettingsKickApiEntries = new()
    {
        ["Settings_Section_KickApi"] = "API Kick",
        ["Settings_KickApi_LoginInfo"] = "Connectez-vous avec Kick pour utiliser les outils de modération Kick dans votre chat.",
        ["Settings_KickApi_Connected"] = "Connecté en tant que {0}",
        ["Settings_KickApi_NotLoggedIn"] = "Vous n'êtes pas connecté avec Kick.",
        ["Settings_KickApi_OpeningBrowser"] = "Ouverture du navigateur pour se connecter...",
        ["Settings_KickApi_LoginFailed"] = "Échec de la connexion. Réessayez.",
        ["Common_LoginWithKick"] = "Se connecter avec Kick",
        ["Kick_ScopesChangedTitle"] = "Nouvelles autorisations Kick",
        ["Kick_ScopesChangedMessage"] = "Cette mise à jour ajoute des fonctions qui nécessitent de nouvelles autorisations Kick, votre session Kick a donc été fermée. Reconnectez-vous pour les activer.",
        ["Kick_DeleteNeedsRelogin"] = "Pour modérer le chat Kick, déconnectez-vous puis reconnectez-vous avec Kick (de nouvelles autorisations sont nécessaires).",
        ["Kick_MsgNeedLogin"] = "Pour modérer les messages Kick, connectez-vous avec Kick dans Paramètres > API Kick.",
        ["Moderation_TabSanctions"] = "Sanctions",
        ["Kick_PermissionMissing"] = "Autorisation Kick manquante : {0}. Déconnectez-vous puis reconnectez-vous à Kick en la cochant sur l'écran d'autorisation.",
        ["Kick_SanctionsNote"] = "Kick ne fournit pas de liste des utilisateurs sanctionnés : voici les bannissements et exclusions temporaires vus depuis l'ouverture de ce panneau.",
        ["Kick_SanctionsEmpty"] = "Aucune sanction enregistrée.",
        ["Auth_BrowserSuccess"] = "C'est fait, vous pouvez fermer cet onglet.",
        ["Auth_BrowserFailed"] = "Échec de la connexion. Vous pouvez fermer cet onglet.",
    };
}
