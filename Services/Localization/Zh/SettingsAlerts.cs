namespace TTNOverlay.Services;

internal static partial class ZhStrings
{
    private static readonly Dictionary<string, string> SettingsAlertsEntries = new()
    {
        ["Settings_Alerts_Header"] = "提醒",
        ["Settings_Alerts_WavInfo"] = "仅支持 .wav 文件。",
        ["Settings_Alerts_MessageSound"] = "新消息时播放声音",
        ["Settings_Alerts_PresetSound"] = "预设声音：",
        ["Settings_Alerts_CustomSound"] = "自定义声音：",
        ["Settings_Alerts_EventSound"] = "事件时播放声音",
        ["Settings_Alerts_Test"] = "测试",
        ["Settings_Alerts_VisualFlash"] = "事件时视觉闪动",
        ["Settings_Alerts_NoCooldown"] = "禁用提醒之间的冷却",
        ["Settings_Alerts_FlashColor"] = "闪动颜色：",
        ["Settings_Alerts_PickColor"] = "选择……",
        ["Settings_Alerts_ShowIrcGif"] = "在 IRC 事件中显示 GIF/图像",
        ["Settings_Alerts_IrcGifPath"] = "通用 GIF/图像",
        ["Settings_Alerts_RemoveGifTooltip"] = "移除",
        ["Settings_Alerts_GifAdvancedMode"] = "按事件类型自定义 GIF/图像",
        ["Settings_Alerts_RemoveCustomGifTooltip"] = "移除",
        ["Settings_Alerts_ResetAllGifs"] = "重置所有自定义 GIF",
        ["Settings_Alerts_BoxColorHeader"] = "事件框颜色",
        ["Settings_Alerts_ColorModeExplanation"] = "“主题”跟随主题（暗色中为纯黑，亮色中为纯浅灰）。“原始”始终使用事件类型自身的颜色，不随主题变化。“自定义”使用您选择的颜色。",
        ["Settings_Alerts_ColorMode_Theme"] = "主题",
        ["Settings_Alerts_ColorMode_Original"] = "原始",
        ["Settings_Alerts_ColorMode_Custom"] = "自定义",
        ["Settings_Alerts_ChooseEllipsis"] = "选择……",
        ["Settings_Alerts_CustomizeBoxColor"] = "按事件类型自定义颜色",
        ["Settings_Alerts_ResetAllColors"] = "重置所有颜色",
    };
}