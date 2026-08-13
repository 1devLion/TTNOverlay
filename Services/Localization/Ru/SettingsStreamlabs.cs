namespace TTNOverlay.Services;

internal static partial class RuStrings
{
    private static readonly Dictionary<string, string> SettingsStreamlabsEntries = new()
    {
        ["Settings_Streamlabs_Header"] = "Streamlabs",
        ["Settings_Streamlabs_Info"] = "Приносит пожертвования, подписки, хосты и мерч, которые не поступают через IRC, без необходимости Twitch OAuth.",
        ["Settings_Streamlabs_Enable"] = "Включить события Streamlabs",
        ["Settings_Streamlabs_SocketToken"] = "Токен Socket API",
        ["Settings_Streamlabs_WidgetToken"] = "Токен виджета",
        ["Settings_Streamlabs_WidgetTokenInfo"] = "Используется для получения сохранённой текстовой/графической конфигурации Alert Box.",
        ["Settings_Streamlabs_SourceLabel"] = "Источник для перекрывающихся оповещений (подписка/повторная подписка/подаренная подписка/рейд)",
        ["Settings_Streamlabs_SourceBoth"] = "Оба (предпочтение Streamlabs)",
        ["Settings_Streamlabs_SourceIrcOnly"] = "Только IRC",
        ["Settings_Streamlabs_SourceStreamlabsOnly"] = "Только Streamlabs",
        ["Settings_Streamlabs_SourceInfo"] = "В режиме «Оба», если Streamlabs включён, его версия предпочтительна для перекрывающихся событий (она приносит серию, пользовательское изображение и т. д.); IRC используется только для того, что Streamlabs не может передать (ритуал, объявления, значок битов).",
    };
}