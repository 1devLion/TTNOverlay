namespace TTNOverlay.Services;

internal static partial class EsStrings
{
    private static readonly Dictionary<string, string> SettingsTwitchApiEntries = new()
    {
        ["Settings_TwitchApi_Enable"] = "Activar API de Twitch",
        ["Settings_TwitchApi_LoginInfo"] = "Iniciá sesión con Twitch para activar el panel de moderación, el widget de espectadores y las insignias.",
        ["Settings_TwitchApi_ShowViewerCount"] = "Mostrar cantidad de espectadores",
        ["Settings_TwitchApi_ViewerCountBackground"] = "Fondo del contador de espectadores",
        ["Settings_TwitchApi_ViewerCountSize"] = "Tamaño del contador de espectadores",
        ["Settings_TwitchApi_ResetViewerCountColor"] = "Restablecer color del tema",
        ["Settings_TwitchApi_ShowBadges"] = "Mostrar insignias",
        ["Settings_TwitchApi_NotLoggedIn"] = "No iniciaste sesión con Twitch.",
        ["Settings_TwitchApi_Connected"] = "Conectado como {0}",
        ["Settings_TwitchApi_OpeningBrowser"] = "Abriendo el navegador para iniciar sesión...",
        ["Settings_TwitchApi_LoginFailed"] = "No se pudo iniciar sesión. Intentá de nuevo.",
    };
}