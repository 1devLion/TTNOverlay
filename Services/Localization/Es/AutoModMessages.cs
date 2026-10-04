namespace TTNOverlay.Services;

internal static partial class EsStrings
{
    private static readonly Dictionary<string, string> AutoModMessagesEntries = new()
    {
        ["Moderation_TabAutoMod"] = "AutoMod",
        ["Moderation_TabAutoModCount"] = "AutoMod ({0})",
        ["Moderation_AutoModNeedLogin"] = "Iniciá sesión con una cuenta de moderador (pestaña Chatters) para revisar los mensajes retenidos por AutoMod.",
        ["Moderation_AutoModEmpty"] = "AutoMod no está reteniendo ningún mensaje.",
        ["Moderation_AutoModConnecting"] = "Conectando con AutoMod...",
        ["Moderation_AutoModUnavailable"] = "La revisión de AutoMod no está disponible. Cerrá sesión y volvé a iniciar sesión con Twitch, y verificá que esta cuenta sea moderadora del canal.",
        ["Moderation_AutoModAllow"] = "Permitir",
        ["Moderation_AutoModDeny"] = "Rechazar",
        ["Moderation_AutoModReasonGeneric"] = "Retenido por AutoMod",
        ["Moderation_AutoModReasonBlockedTerm"] = "Retenido: contiene un término bloqueado",
        ["Moderation_AutoModReasonCategory"] = "Retenido por AutoMod: {0}",
        ["Moderation_AutoModReasonCategoryLevel"] = "Retenido por AutoMod: {0} (nivel {1})",
        ["Moderation_AutoModAllowed"] = "Mensaje de {0} permitido.",
        ["Moderation_AutoModDenied"] = "Mensaje de {0} rechazado.",
        ["Moderation_AutoModAlreadyResolved"] = "El mensaje de {0} ya fue resuelto.",
        ["Moderation_AutoModFailed"] = "No se pudo actualizar el mensaje de {0}.",
        ["Moderation_AutoModNeedsRelogin"] = "Para revisar mensajes de AutoMod, cerrá sesión y volvé a iniciar sesión con Twitch (hace falta un permiso nuevo).",
        ["EventMsg_AutoModHeld"] = "AutoMod está reteniendo un mensaje de {0}.",
    };
}
