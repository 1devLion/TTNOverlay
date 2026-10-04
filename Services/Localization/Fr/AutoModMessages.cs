namespace TTNOverlay.Services;

internal static partial class FrStrings
{
    private static readonly Dictionary<string, string> AutoModMessagesEntries = new()
    {
        ["Moderation_TabAutoMod"] = "AutoMod",
        ["Moderation_TabAutoModCount"] = "AutoMod ({0})",
        ["Moderation_AutoModNeedLogin"] = "Connectez-vous avec un compte modérateur (onglet Chatteurs) pour examiner les messages retenus par AutoMod.",
        ["Moderation_AutoModEmpty"] = "AutoMod ne retient aucun message.",
        ["Moderation_AutoModConnecting"] = "Connexion à AutoMod...",
        ["Moderation_AutoModUnavailable"] = "L'examen AutoMod n'est pas disponible. Déconnectez-vous puis reconnectez-vous à Twitch, et vérifiez que ce compte est modérateur de la chaîne.",
        ["Moderation_AutoModAllow"] = "Autoriser",
        ["Moderation_AutoModDeny"] = "Refuser",
        ["Moderation_AutoModReasonGeneric"] = "Retenu par AutoMod",
        ["Moderation_AutoModReasonBlockedTerm"] = "Retenu : contient un terme bloqué",
        ["Moderation_AutoModReasonCategory"] = "Retenu par AutoMod : {0}",
        ["Moderation_AutoModReasonCategoryLevel"] = "Retenu par AutoMod : {0} (niveau {1})",
        ["Moderation_AutoModAllowed"] = "Message de {0} autorisé.",
        ["Moderation_AutoModDenied"] = "Message de {0} refusé.",
        ["Moderation_AutoModAlreadyResolved"] = "Le message de {0} a déjà été traité.",
        ["Moderation_AutoModFailed"] = "Impossible de mettre à jour le message de {0}.",
        ["Moderation_AutoModNeedsRelogin"] = "Pour examiner les messages AutoMod, déconnectez-vous puis reconnectez-vous à Twitch (une nouvelle autorisation est requise).",
        ["EventMsg_AutoModHeld"] = "AutoMod retient un message de {0}.",
    };
}
