namespace TTNOverlay.Services;

internal static partial class EsStrings
{
    private static readonly Dictionary<string, string> SettingsAboutEntries = new()
    {
        ["Settings_About_SupportUs"] = "Apoyanos ❤️",
        ["Settings_About_Author"] = "Autor: ",
        ["Settings_About_License"] = "Licencia: ",
        ["Settings_About_LicenseText"] = "Software de código abierto. Podés usar, copiar, modificar y distribuir este programa libremente, siempre que se mantenga el aviso de copyright original.",
        ["Settings_About_VersionFormat"] = "Versión {0} ({1} bits)",
        ["Settings_About_DebugMode"] = "Modo debug",
        ["Settings_About_DebugModeWarning"] = "Activar el modo debug afecta el rendimiento (requiere reiniciar). Actívalo solo para diagnosticar un problema puntual y desactívalo después. El archivo de registro se guarda en %appdata%\\TTNOverlay\\debug.log.",
    };
}