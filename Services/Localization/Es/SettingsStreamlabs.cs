namespace TTNOverlay.Services;

internal static partial class EsStrings
{
    private static readonly Dictionary<string, string> SettingsStreamlabsEntries = new()
    {
        ["Settings_Streamlabs_Header"] = "Streamlabs",
        ["Settings_Streamlabs_Info"] = "Trae donaciones, follows, hosts y merch que no llegan por IRC, sin necesitar OAuth de Twitch.",
        ["Settings_Streamlabs_Enable"] = "Activar eventos de Streamlabs",
        ["Settings_Streamlabs_SocketToken"] = "Token de la Socket API",
        ["Settings_Streamlabs_WidgetToken"] = "Token del widget",
        ["Settings_Streamlabs_WidgetTokenInfo"] = "Se usa para traer la configuración de texto/imagen guardada del Alert Box.",
        ["Settings_Streamlabs_SourceLabel"] = "Origen para alertas solapadas (sub/resub/subgift/raid)",
        ["Settings_Streamlabs_SourceBoth"] = "Ambos (preferir Streamlabs)",
        ["Settings_Streamlabs_SourceIrcOnly"] = "Solo IRC",
        ["Settings_Streamlabs_SourceStreamlabsOnly"] = "Solo Streamlabs",
        ["Settings_Streamlabs_SourceInfo"] = "En \"Ambos\", si Streamlabs está activo se prefiere su versión para lo que se solapa (trae racha, imagen personalizada, etc.); IRC se usa solo para lo que Streamlabs no puede avisar (ritual, anuncios, insignia de bits).",
    };
}
