namespace TTNOverlay.Services;

internal static partial class RuStrings
{
    private static readonly Dictionary<string, string> SettingsGeneralEntries = new()
    {
        ["Settings_Language"] = "Язык",
        ["Settings_Language_English"] = "Английский",
        ["Settings_Language_Spanish"] = "Испанский",
        ["Settings_WindowTitle"] = "Настройки",
        ["Settings_Section_General"] = "Общие",
        ["Settings_Section_Hotkeys"] = "Горячие клавиши",
        ["Settings_Section_TwitchApi"] = "Twitch API",
        ["Settings_Section_Streamlabs"] = "Streamlabs",
        ["Settings_Section_Alerts"] = "Оповещения",
        ["Settings_Section_Audio"] = "Аудио",
        ["Settings_Section_About"] = "О программе",
        ["Settings_Section_ViewerCount"] = "Счётчик зрителей",
        ["Settings_General_Theme"] = "Тема",
        ["Settings_Theme_Dark"] = "Тёмная",
        ["Settings_Theme_Light"] = "Светлая",
        ["Settings_General_Channel"] = "Twitch-канал",
        ["Settings_General_ChatSource"] = "Источник чата",
        ["Settings_ChatSource_Twitch"] = "Twitch",
        ["Settings_ChatSource_Kick"] = "Kick",
        ["Settings_ChatSource_Multichat"] = "Мультичат (Twitch + Kick)",
        ["Settings_General_ChannelKick"] = "Канал Kick",
        ["Settings_General_ChannelShared"] = "Канал",
        ["Settings_General_MultichatEnableTwitch"] = "Включить Twitch",
        ["Settings_General_MultichatEnableKick"] = "Включить Kick",
        ["Settings_General_MultichatUseSameChannel"] = "Использовать одно и то же имя канала для обоих",
        ["Settings_General_FontSize"] = "Размер шрифта",
        ["Settings_General_MessageLifetime"] = "Время жизни сообщений (секунды)",
        ["Settings_General_MessageLifetimeInfo"] = "0 = сообщения никогда не истекают и остаются в чате. Не рекомендуется: использует значительно больше ресурсов.",
        ["Settings_General_MaxMessages"] = "Макс. сообщений на экране",
        ["Settings_General_ClickThrough"] = "Сквозной клик (клики мыши проходят сквозь оверлей)",
        ["Settings_General_DebugMode"] = "Включить журнал отладки",
        ["Settings_General_ThirdPartyEmotes"] = "Сторонние эмоции (BTTV/FFZ/7TV)",
        ["Settings_General_EnableEventsPanel"] = "Включить панель событий",
        ["Settings_General_EnableModerationPanel"] = "Включить панель модерации",
        ["Settings_General_HighQualityMedia"] = "Качественные медиа",
        ["Settings_General_HighQualityMediaInfo"] = "Декодирует анимированные эмоции и оповещения в их исходном разрешении вместо масштабирования. Более чёткое изображение, но использует больше ОЗУ.",
    };
}