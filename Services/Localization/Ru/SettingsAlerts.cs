namespace TTNOverlay.Services;

internal static partial class RuStrings
{
    private static readonly Dictionary<string, string> SettingsAlertsEntries = new()
    {
        ["Settings_Alerts_Header"] = "Оповещения",
        ["Settings_Alerts_WavInfo"] = "Поддерживаются только файлы .wav.",
        ["Settings_Alerts_MessageSound"] = "Звук при новом сообщении",
        ["Settings_Alerts_PresetSound"] = "Предустановленный звук:",
        ["Settings_Alerts_CustomSound"] = "Пользовательский звук:",
        ["Settings_Alerts_EventSound"] = "Звук при событии",
        ["Settings_Alerts_Test"] = "Тест",
        ["Settings_Alerts_VisualFlash"] = "Визуальная вспышка при событии",
        ["Settings_Alerts_NoCooldown"] = "Отключить задержку между оповещениями",
        ["Settings_Alerts_FlashColor"] = "Цвет вспышки:",
        ["Settings_Alerts_PickColor"] = "Выбрать...",
        ["Settings_Alerts_ShowIrcGif"] = "Показывать GIF/изображение при IRC-событиях",
        ["Settings_Alerts_IrcGifPath"] = "Общий GIF/изображение",
        ["Settings_Alerts_RemoveGifTooltip"] = "Удалить",
        ["Settings_Alerts_GifAdvancedMode"] = "Настроить GIF/изображение для каждого типа события",
        ["Settings_Alerts_RemoveCustomGifTooltip"] = "Удалить",
        ["Settings_Alerts_ResetAllGifs"] = "Сбросить все пользовательские GIF",
        ["Settings_Alerts_BoxColorHeader"] = "Цвет блока события",
        ["Settings_Alerts_ColorModeExplanation"] = "\"Тема\" следует теме (сплошной чёрный в тёмной теме, собственный цвет в светлой). \"Оригинал\" всегда использует собственный цвет типа события независимо от темы. \"Пользовательский\" использует выбранный вами цвет.",
        ["Settings_Alerts_ColorMode_Theme"] = "Тема",
        ["Settings_Alerts_ColorMode_Original"] = "Оригинал",
        ["Settings_Alerts_ColorMode_Custom"] = "Пользовательский",
        ["Settings_Alerts_ChooseEllipsis"] = "Выбрать...",
        ["Settings_Alerts_CustomizeBoxColor"] = "Настроить цвет для каждого типа события",
        ["Settings_Alerts_ResetAllColors"] = "Сбросить все цвета",
    };
}