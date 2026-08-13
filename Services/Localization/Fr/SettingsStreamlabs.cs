namespace TTNOverlay.Services;

internal static partial class FrStrings
{
    private static readonly Dictionary<string, string> SettingsStreamlabsEntries = new()
    {
        ["Settings_Streamlabs_Header"] = "Streamlabs",
        ["Settings_Streamlabs_Info"] = "Amène les dons, follows, hôtes et merch qui ne passent pas par IRC, sans nécessiter d'OAuth Twitch.",
        ["Settings_Streamlabs_Enable"] = "Activer les événements Streamlabs",
        ["Settings_Streamlabs_SocketToken"] = "Jeton de l'API Socket",
        ["Settings_Streamlabs_WidgetToken"] = "Jeton du widget",
        ["Settings_Streamlabs_WidgetTokenInfo"] = "Utilisé pour récupérer la configuration texte/image enregistrée de l'Alert Box.",
        ["Settings_Streamlabs_SourceLabel"] = "Source pour les alertes qui se chevauchent (sub/resub/subgift/raid)",
        ["Settings_Streamlabs_SourceBoth"] = "Les deux (préférer Streamlabs)",
        ["Settings_Streamlabs_SourceIrcOnly"] = "IRC uniquement",
        ["Settings_Streamlabs_SourceStreamlabsOnly"] = "Streamlabs uniquement",
        ["Settings_Streamlabs_SourceInfo"] = "En mode « Les deux », si Streamlabs est activé, sa version est privilégiée pour les événements qui se chevauchent (il apporte la série, l'image personnalisée, etc.) ; IRC n'est utilisé que pour ce que Streamlabs ne peut pas rapporter (rituel, annonces, badge bits).",
    };
}