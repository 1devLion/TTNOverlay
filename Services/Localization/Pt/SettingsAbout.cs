namespace TTNOverlay.Services;

internal static partial class PtStrings
{
    private static readonly Dictionary<string, string> SettingsAboutEntries = new()
    {
        ["Settings_About_SupportUs"] = "Apoie-nos ❤️",
        ["Settings_About_Author"] = "Autor: ",
        ["Settings_About_License"] = "Licença: ",
        ["Settings_About_LicenseText"] = "Software de código aberto. Você pode usar, copiar, modificar e distribuir este programa livremente, desde que seja mantido o aviso de copyright original.",
        ["Settings_About_VersionFormat"] = "Versão {0} ({1} bits)",
        ["Settings_About_DebugMode"] = "Modo debug",
        ["Settings_About_DebugModeWarning"] = "Ativar o modo debug afeta o desempenho (requer reinício). Ative apenas para diagnosticar um problema específico e depois desative. O arquivo de log é salvo em %appdata%\\TTNOverlay\\debug.log.",
    };
}