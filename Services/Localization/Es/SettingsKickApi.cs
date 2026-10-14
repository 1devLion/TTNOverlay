namespace TTNOverlay.Services;

internal static partial class EsStrings
{
    private static readonly Dictionary<string, string> SettingsKickApiEntries = new()
    {
        ["Settings_Section_KickApi"] = "API de Kick",
        ["Settings_KickApi_LoginInfo"] = "Iniciá sesión con Kick para usar las herramientas de moderación de Kick en tu chat.",
        ["Settings_KickApi_Connected"] = "Conectado como {0}",
        ["Settings_KickApi_NotLoggedIn"] = "No iniciaste sesión con Kick.",
        ["Settings_KickApi_OpeningBrowser"] = "Abriendo el navegador para iniciar sesión...",
        ["Settings_KickApi_LoginFailed"] = "No se pudo iniciar sesión. Intentá de nuevo.",
        ["Common_LoginWithKick"] = "Iniciar sesión con Kick",
        ["Kick_ScopesChangedTitle"] = "Permisos nuevos de Kick",
        ["Kick_ScopesChangedMessage"] = "Esta actualización agrega funciones que necesitan permisos nuevos de Kick, así que se cerró tu sesión de Kick. Volvé a iniciar sesión para activarlos.",
        ["Kick_DeleteNeedsRelogin"] = "Para moderar el chat de Kick, cerrá sesión y volvé a iniciar sesión con Kick (hacen falta permisos nuevos).",
        ["Kick_MsgNeedLogin"] = "Para moderar mensajes de Kick, iniciá sesión con Kick en la pestaña Stream (o en Configuración > API de Kick).",
        ["Moderation_TabSanctions"] = "Sanciones",
        ["Kick_PermissionMissing"] = "Falta el permiso {0} de Kick. Cerrá sesión y volvé a iniciar sesión en Kick marcándolo en la pantalla de permisos.",
        ["Kick_SanctionsNote"] = "Kick no ofrece una lista de sancionados: acá se ven los baneos y muteos registrados desde que se abrió este panel.",
        ["Kick_SanctionsEmpty"] = "No hay sanciones registradas.",
        ["Auth_BrowserSuccess"] = "Listo, ya podés cerrar esta pestaña.",
        ["Auth_BrowserFailed"] = "No se pudo iniciar sesión. Podés cerrar esta pestaña.",
    };
}
