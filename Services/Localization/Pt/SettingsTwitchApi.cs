namespace TTNOverlay.Services;

internal static partial class PtStrings
{
    private static readonly Dictionary<string, string> SettingsTwitchApiEntries = new()
    {
        ["Settings_TwitchApi_Enable"] = "Ativar API do Twitch",
        ["Settings_TwitchApi_LoginInfo"] = "Faça login com Twitch para ativar o painel de moderação, o widget de espectadores e os emblemas.",
        ["Settings_TwitchApi_ShowViewerCount"] = "Mostrar quantidade de espectadores",
        ["Settings_TwitchApi_ViewerCountMode"] = "Exibir espectadores como",
        ["Settings_ViewerCountMode_Sum"] = "Total (soma de todas as plataformas)",
        ["Settings_ViewerCountMode_PerPlatform"] = "Personalizado",
        ["Settings_TwitchApi_ViewerCountIncludeTwitch"] = "Incluir Twitch",
        ["Settings_TwitchApi_ViewerCountIncludeKick"] = "Incluir Kick",
        ["Settings_TwitchApi_ViewerCountIncludeYouTube"] = "Incluir YouTube",
        ["Settings_TwitchApi_ViewerCountBackground"] = "Fundo do contador de espectadores",
        ["Settings_TwitchApi_ViewerCountTextColor"] = "Cor do texto do contador de espectadores",
        ["Settings_TwitchApi_ViewerCountSize"] = "Tamanho do contador de espectadores",
        ["Settings_TwitchApi_ResetViewerCountColor"] = "Redefinir cor do tema",
        ["Settings_TwitchApi_ShowBadges"] = "Mostrar emblemas",
        ["Settings_TwitchApi_NotLoggedIn"] = "Você não está conectado com Twitch.",
        ["Settings_TwitchApi_Connected"] = "Conectado como {0}",
        ["Settings_TwitchApi_OpeningBrowser"] = "Abrindo navegador para login...",
        ["Settings_TwitchApi_LoginFailed"] = "Não foi possível fazer login. Tente novamente.",
    };
}