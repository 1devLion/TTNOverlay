namespace TTNOverlay.Services;

internal static partial class FrStrings
{
    private static readonly Dictionary<string, string> SettingsGeneralEntries = new()
    {
        ["Settings_Language"] = "Langue",
        ["Settings_Language_English"] = "Anglais",
        ["Settings_Language_Spanish"] = "Espagnol",
        ["Settings_WindowTitle"] = "Paramètres",
        ["Settings_Section_General"] = "Général",
        ["Settings_Section_Hotkeys"] = "Raccourcis clavier",
        ["Settings_Section_TwitchApi"] = "API Twitch",
        ["Settings_Section_Streamlabs"] = "Streamlabs",
        ["Settings_Section_Alerts"] = "Alertes",
        ["Settings_Section_Audio"] = "Audio",
        ["Settings_Section_About"] = "À propos",
        ["Settings_Section_ViewerCount"] = "Compteur de spectateurs",
        ["Settings_General_Theme"] = "Thème",
        ["Settings_Theme_Dark"] = "Sombre",
        ["Settings_Theme_Light"] = "Clair",
        ["Settings_General_Channel"] = "Chaîne Twitch",
        ["Settings_General_ChatSource"] = "Source du chat",
        ["Settings_ChatSource_Twitch"] = "Twitch",
        ["Settings_ChatSource_Kick"] = "Kick",
        ["Settings_ChatSource_Multichat"] = "Multichat (Twitch + Kick)",
        ["Settings_General_ChannelKick"] = "Chaîne Kick",
        ["Settings_General_ChannelShared"] = "Chaîne",
        ["Settings_General_MultichatEnableTwitch"] = "Activer Twitch",
        ["Settings_General_MultichatEnableKick"] = "Activer Kick",
        ["Settings_General_MultichatUseSameChannel"] = "Utiliser le même nom de chaîne pour les deux",
        ["Settings_General_FontSize"] = "Taille de la police",
        ["Settings_General_MessageLifetime"] = "Durée de vie des messages (secondes)",
        ["Settings_General_MessageLifetimeInfo"] = "0 = les messages n'expirent jamais et restent dans le chat. Déconseillé : consommation de ressources beaucoup plus élevée.",
        ["Settings_General_MaxMessages"] = "Nb max de messages à l'écran",
        ["Settings_General_ClickThrough"] = "Transparent aux clics (les clics de souris traversent l'overlay)",
        ["Settings_General_DebugMode"] = "Activer le journal de débogage",
        ["Settings_General_ThirdPartyEmotes"] = "Émoticônes tierces (BTTV/FFZ/7TV)",
        ["Settings_General_EnableEventsPanel"] = "Activer le panneau d'événements",
        ["Settings_General_EnableModerationPanel"] = "Activer le panneau de modération",
        ["Settings_General_HighQualityMedia"] = "Médias haute qualité",
        ["Settings_General_HighQualityMediaInfo"] = "Décode les émoticônes animées et les alertes dans leur résolution native au lieu de les réduire. Plus net, mais utilise plus de RAM.",
    };
}