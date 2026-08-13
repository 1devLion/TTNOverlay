namespace TTNOverlay.Services;

internal static partial class PtStrings
{
    private static readonly Dictionary<string, string> SettingsGeneralEntries = new()
    {
        ["Settings_Language"] = "Idioma",
        ["Settings_Language_English"] = "Inglês",
        ["Settings_Language_Spanish"] = "Espanhol",
        ["Settings_WindowTitle"] = "Configurações",
        ["Settings_Section_General"] = "Geral",
        ["Settings_Section_Hotkeys"] = "Atalhos de teclado",
        ["Settings_Section_TwitchApi"] = "API do Twitch",
        ["Settings_Section_Streamlabs"] = "Streamlabs",
        ["Settings_Section_Alerts"] = "Alertas",
        ["Settings_Section_Audio"] = "Áudio",
        ["Settings_Section_About"] = "Sobre",
        ["Settings_General_Theme"] = "Tema",
        ["Settings_Theme_Dark"] = "Escuro",
        ["Settings_Theme_Light"] = "Claro",
        ["Settings_General_Channel"] = "Canal do Twitch",
        ["Settings_General_FontSize"] = "Tamanho da fonte",
        ["Settings_General_MessageLifetime"] = "Duração das mensagens (segundos)",
        ["Settings_General_MessageLifetimeInfo"] = "0 = as mensagens nunca expiram e permanecem no chat. Não recomendado: usa muito mais recursos.",
        ["Settings_General_MaxMessages"] = "Máx. de mensagens na tela",
        ["Settings_General_ClickThrough"] = "Click-through (cliques do mouse atravessam o overlay)",
        ["Settings_General_DebugMode"] = "Ativar log de depuração",
        ["Settings_General_ThirdPartyEmotes"] = "Emotes de terceiros (BTTV/FFZ/7TV)",
        ["Settings_General_EnableEventsPanel"] = "Ativar painel de eventos",
        ["Settings_General_EnableModerationPanel"] = "Ativar painel de moderação",
        ["Settings_General_HighQualityMedia"] = "Mídia de alta qualidade",
        ["Settings_General_HighQualityMediaInfo"] = "Decodifica emotes e alertas animados em sua resolução nativa em vez de reduzi-los. Fica mais nítido, mas usa mais RAM.",
    };
}