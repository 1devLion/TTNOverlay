namespace TTNOverlay.Services;

internal static partial class ZhStrings
{
    private static readonly Dictionary<string, string> SettingsStreamlabsEntries = new()
    {
        ["Settings_Streamlabs_Header"] = "Streamlabs",
        ["Settings_Streamlabs_Info"] = "可接收不经过 IRC 的捐赠、关注、Host 和周边商品事件，无需 Twitch OAuth。",
        ["Settings_Streamlabs_Enable"] = "启用 Streamlabs 事件",
        ["Settings_Streamlabs_SocketToken"] = "Socket API 令牌",
        ["Settings_Streamlabs_WidgetToken"] = "Widget 令牌",
        ["Settings_Streamlabs_WidgetTokenInfo"] = "用于获取提醒框保存的文本/图像配置。",
        ["Settings_Streamlabs_SourceLabel"] = "重叠事件（订阅/续订/赠送订阅/Raid）的数据源",
        ["Settings_Streamlabs_SourceBoth"] = "两者兼顾（优先 Streamlabs）",
        ["Settings_Streamlabs_SourceIrcOnly"] = "仅 IRC",
        ["Settings_Streamlabs_SourceStreamlabsOnly"] = "仅 Streamlabs",
        ["Settings_Streamlabs_SourceInfo"] = "在“两者兼顾”模式下，如果启用了 Streamlabs，则会优先使用其版本用于重叠事件（它带有连续订阅、自定义图像等）；仅当 Streamlabs 无法报告时才使用 IRC（如仪式、公告、Bits 徽章）。",
    };
}