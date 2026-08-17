namespace TTNOverlay.Services;

internal static partial class RuStrings
{
    private static readonly Dictionary<string, string> SettingsTwitchApiEntries = new()
    {
        ["Settings_TwitchApi_Enable"] = "Включить Twitch API",
        ["Settings_TwitchApi_LoginInfo"] = "Войдите с Twitch, чтобы включить панель модерации, виджет количества зрителей и значки.",
        ["Settings_TwitchApi_ShowViewerCount"] = "Показывать количество зрителей",
        ["Settings_TwitchApi_ViewerCountMode"] = "Отображение количества зрителей",
        ["Settings_ViewerCountMode_Sum"] = "Итого (сумма по всем платформам)",
        ["Settings_ViewerCountMode_PerPlatform"] = "Настраиваемый",
        ["Settings_TwitchApi_ViewerCountIncludeTwitch"] = "Учитывать Twitch",
        ["Settings_TwitchApi_ViewerCountIncludeKick"] = "Учитывать Kick",
        ["Settings_TwitchApi_ViewerCountIncludeYouTube"] = "Учитывать YouTube",
        ["Settings_TwitchApi_ViewerCountBackground"] = "Фон счётчика зрителей",
        ["Settings_TwitchApi_ViewerCountTextColor"] = "Цвет текста счётчика зрителей",
        ["Settings_TwitchApi_ViewerCountSize"] = "Размер счётчика зрителей",
        ["Settings_TwitchApi_ResetViewerCountColor"] = "Сбросить цвет темы",
        ["Settings_TwitchApi_ShowBadges"] = "Показывать значки",
        ["Settings_TwitchApi_NotLoggedIn"] = "Вы не вошли в Twitch.",
        ["Settings_TwitchApi_Connected"] = "Подключено как {0}",
        ["Settings_TwitchApi_OpeningBrowser"] = "Открытие браузера для входа...",
        ["Settings_TwitchApi_LoginFailed"] = "Не удалось войти. Попробуйте снова.",
    };
}