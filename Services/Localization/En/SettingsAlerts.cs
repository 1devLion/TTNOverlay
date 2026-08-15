namespace TTNOverlay.Services;

internal static partial class EnStrings
{
    private static readonly Dictionary<string, string> SettingsAlertsEntries = new()
    {
        ["Settings_Alerts_Header"] = "Alerts",
        ["Settings_Alerts_WavInfo"] = "Only .wav files are supported.",
        ["Settings_Alerts_MessageSound"] = "Sound on new message",
        ["Settings_Alerts_PresetSound"] = "Preset sound:",
        ["Settings_Alerts_CustomSound"] = "Custom sound:",
        ["Settings_Alerts_EventSound"] = "Sound on event",
        ["Settings_Alerts_Test"] = "Test",
        ["Settings_Alerts_VisualFlash"] = "Visual flash on event",
        ["Settings_Alerts_NoCooldown"] = "Disable cooldown between alerts",
        ["Settings_Alerts_FlashColor"] = "Flash color:",
        ["Settings_Alerts_PickColor"] = "Choose...",
        ["Settings_Alerts_ShowIrcGif"] = "Show GIF/image on IRC events",
        ["Settings_Alerts_IrcGifPath"] = "Generic GIF/image",
        ["Settings_Alerts_RemoveGifTooltip"] = "Remove",
        ["Settings_Alerts_GifAdvancedMode"] = "Customize GIF/image per event type",
        ["Settings_Alerts_RemoveCustomGifTooltip"] = "Remove",
        ["Settings_Alerts_ResetAllGifs"] = "Reset all custom GIFs",
        ["Settings_Alerts_BoxColorHeader"] = "Event box color",
        ["Settings_Alerts_ColorModeExplanation"] = "\"Theme\" follows the theme (solid black in Dark, solid light gray in Light). \"Original\" always uses the event type's own color, regardless of theme. \"Custom\" uses the color you pick.",
        ["Settings_Alerts_ColorMode_Theme"] = "Theme",
        ["Settings_Alerts_ColorMode_Original"] = "Original",
        ["Settings_Alerts_ColorMode_Custom"] = "Custom",
        ["Settings_Alerts_ChooseEllipsis"] = "Choose...",
        ["Settings_Alerts_CustomizeBoxColor"] = "Customize color per event type",
        ["Settings_Alerts_ResetAllColors"] = "Reset all colors",
    };
}