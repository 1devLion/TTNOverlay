namespace TTNOverlay.Services;

internal static partial class EsStrings
{
    private static readonly Dictionary<string, string> SettingsGeneralEntries = new()
    {
        ["Settings_Language"] = "Idioma",
        ["Settings_Language_English"] = "Inglés",
        ["Settings_Language_Spanish"] = "Español",
        ["Settings_WindowTitle"] = "Configuración",
        ["Settings_Section_General"] = "General",
        ["Settings_Section_Hotkeys"] = "Atajos de teclado",
        ["Settings_Section_TwitchApi"] = "API de Twitch",
        ["Settings_Section_Streamlabs"] = "Streamlabs",
        ["Settings_Section_Alerts"] = "Alertas",
        ["Settings_Section_Audio"] = "Audio",
        ["Settings_Section_About"] = "Acerca de",
        ["Settings_General_Theme"] = "Tema",
        ["Settings_Theme_Dark"] = "Oscuro",
        ["Settings_Theme_Light"] = "Claro",
        ["Settings_General_Channel"] = "Canal de Twitch",
        ["Settings_General_FontSize"] = "Tamaño de fuente",
        ["Settings_General_MessageLifetime"] = "Duración de los mensajes (segundos)",
        ["Settings_General_MessageLifetimeInfo"] = "0 = los mensajes nunca expiran y quedan en el chat. No recomendado: usa muchos más recursos.",
        ["Settings_General_MaxMessages"] = "Máx. de mensajes en pantalla",
        ["Settings_General_ClickThrough"] = "Click-through (los clics del mouse atraviesan el overlay)",
        ["Settings_General_DebugMode"] = "Activar log de depuración",
        ["Settings_General_ThirdPartyEmotes"] = "Emotes de terceros (BTTV/FFZ/7TV)",
        ["Settings_General_EnableEventsPanel"] = "Activar panel de eventos",
        ["Settings_General_EnableModerationPanel"] = "Activar panel de moderación",
        ["Settings_General_HighQualityMedia"] = "Medios de alta calidad",
        ["Settings_General_HighQualityMediaInfo"] = "Decodifica emotes y alertas animadas en su resolución nativa en vez de reducirlas. Se ve más nítido, pero usa más RAM.",
    };
}
