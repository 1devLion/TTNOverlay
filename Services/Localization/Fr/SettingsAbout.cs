namespace TTNOverlay.Services;

internal static partial class FrStrings
{
    private static readonly Dictionary<string, string> SettingsAboutEntries = new()
    {
        ["Settings_About_SupportUs"] = "Soutenez-nous ❤️",
        ["Settings_About_Author"] = "Auteur : ",
        ["Settings_About_License"] = "Licence : ",
        ["Settings_About_LicenseText"] = "Logiciel open-source. Vous pouvez librement utiliser, copier, modifier et distribuer ce programme, à condition de conserver la mention de copyright originale.",
        ["Settings_About_VersionFormat"] = "Version {0} ({1} bits)",
        ["Settings_About_DebugMode"] = "Mode débogage",
        ["Settings_About_DebugModeWarning"] = "Activer le mode débogage affecte les performances (nécessite un redémarrage). Ne l'activez que pour diagnostiquer un problème précis, puis désactivez-le. Le fichier journal est enregistré dans %appdata%\\TTNOverlay\\debug.log.",
    };
}