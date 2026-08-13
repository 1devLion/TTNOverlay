namespace TTNOverlay.Services;

internal static partial class EsStrings
{
    private static readonly Dictionary<string, string> SettingsAlertsEntries = new()
    {
        ["Settings_Alerts_Header"] = "Alertas",
        ["Settings_Alerts_WavInfo"] = "Solo se admiten archivos .wav.",
        ["Settings_Alerts_MessageSound"] = "Sonido en mensaje nuevo",
        ["Settings_Alerts_PresetSound"] = "Sonido predefinido:",
        ["Settings_Alerts_CustomSound"] = "Sonido personalizado:",
        ["Settings_Alerts_EventSound"] = "Sonido en evento",
        ["Settings_Alerts_Test"] = "Probar",
        ["Settings_Alerts_VisualFlash"] = "Destello visual en evento",
        ["Settings_Alerts_NoCooldown"] = "Desactivar cooldown entre alertas",
        ["Settings_Alerts_FlashColor"] = "Color del destello:",
        ["Settings_Alerts_PickColor"] = "Elegir...",
        ["Settings_Alerts_ShowIrcGif"] = "Mostrar GIF/imagen en eventos de IRC",
        ["Settings_Alerts_IrcGifPath"] = "GIF/imagen genérico",
        ["Settings_Alerts_RemoveGifTooltip"] = "Quitar",
        ["Settings_Alerts_GifAdvancedMode"] = "Personalizar GIF/imagen por tipo de evento",
        ["Settings_Alerts_RemoveCustomGifTooltip"] = "Quitar",
        ["Settings_Alerts_ResetAllGifs"] = "Restablecer todos los GIF personalizados",
        ["Settings_Alerts_BoxColorHeader"] = "Color de la caja de evento",
        ["Settings_Alerts_ColorModeExplanation"] = "\"Predeterminado\" respeta el tema (negro parejo en Oscuro, color propio en Claro). \"Original\" siempre usa el color propio del tipo de evento, sin importar el tema. \"Personalizado\" usa el color que elijas.",
        ["Settings_Alerts_ColorMode_Theme"] = "Predeterminado",
        ["Settings_Alerts_ColorMode_Original"] = "Original",
        ["Settings_Alerts_ColorMode_Custom"] = "Personalizado",
        ["Settings_Alerts_ChooseEllipsis"] = "Elegir...",
        ["Settings_Alerts_CustomizeBoxColor"] = "Personalizar color por tipo de evento",
        ["Settings_Alerts_ResetAllColors"] = "Restablecer todos los colores",
    };
}
