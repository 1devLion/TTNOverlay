namespace TTNOverlay.Services;

internal static partial class PtStrings
{
    private static readonly Dictionary<string, string> SettingsStreamlabsEntries = new()
    {
        ["Settings_Streamlabs_Header"] = "Streamlabs",
        ["Settings_Streamlabs_Info"] = "Traz doações, follows, hosts e merch que não chegam pelo IRC, sem precisar de OAuth do Twitch.",
        ["Settings_Streamlabs_Enable"] = "Ativar eventos do Streamlabs",
        ["Settings_Streamlabs_SocketToken"] = "Token da Socket API",
        ["Settings_Streamlabs_WidgetToken"] = "Token do widget",
        ["Settings_Streamlabs_WidgetTokenInfo"] = "Usado para trazer a configuração de texto/imagem salva do Alert Box.",
        ["Settings_Streamlabs_SourceLabel"] = "Origem para alertas sobrepostos (sub/resub/subgift/raid)",
        ["Settings_Streamlabs_SourceBoth"] = "Ambos (preferir Streamlabs)",
        ["Settings_Streamlabs_SourceIrcOnly"] = "Apenas IRC",
        ["Settings_Streamlabs_SourceStreamlabsOnly"] = "Apenas Streamlabs",
        ["Settings_Streamlabs_SourceInfo"] = "Em \"Ambos\", se Streamlabs estiver ativo, sua versão é preferida para o que se sobrepõe (traz sequência, imagem personalizada, etc.); IRC é usado apenas para o que Streamlabs não pode avisar (ritual, anúncios, emblema de bits).",
    };
}