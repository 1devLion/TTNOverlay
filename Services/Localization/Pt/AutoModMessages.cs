namespace TTNOverlay.Services;

internal static partial class PtStrings
{
    private static readonly Dictionary<string, string> AutoModMessagesEntries = new()
    {
        ["Moderation_TabAutoMod"] = "AutoMod",
        ["Moderation_TabAutoModCount"] = "AutoMod ({0})",
        ["Moderation_AutoModNeedLogin"] = "Entre com uma conta de moderador (aba Chatters) para revisar as mensagens retidas pelo AutoMod.",
        ["Moderation_AutoModEmpty"] = "O AutoMod não está retendo nenhuma mensagem.",
        ["Moderation_AutoModConnecting"] = "Conectando ao AutoMod...",
        ["Moderation_AutoModUnavailable"] = "A revisão do AutoMod não está disponível. Saia e entre novamente com a Twitch e confirme que esta conta é moderadora do canal.",
        ["Moderation_AutoModAllow"] = "Permitir",
        ["Moderation_AutoModDeny"] = "Recusar",
        ["Moderation_AutoModReasonGeneric"] = "Retida pelo AutoMod",
        ["Moderation_AutoModReasonBlockedTerm"] = "Retida: contém um termo bloqueado",
        ["Moderation_AutoModReasonCategory"] = "Retida pelo AutoMod: {0}",
        ["Moderation_AutoModReasonCategoryLevel"] = "Retida pelo AutoMod: {0} (nível {1})",
        ["Moderation_AutoModAllowed"] = "Mensagem de {0} permitida.",
        ["Moderation_AutoModDenied"] = "Mensagem de {0} recusada.",
        ["Moderation_AutoModAlreadyResolved"] = "A mensagem de {0} já foi tratada.",
        ["Moderation_AutoModFailed"] = "Não foi possível atualizar a mensagem de {0}.",
        ["Moderation_AutoModNeedsRelogin"] = "Para revisar mensagens do AutoMod, saia e entre novamente com a Twitch (é necessária uma nova permissão).",
        ["EventMsg_AutoModHeld"] = "O retendo uma mensagem de {0}.",
    };
}
