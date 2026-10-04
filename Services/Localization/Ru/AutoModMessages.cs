namespace TTNOverlay.Services;

internal static partial class RuStrings
{
    private static readonly Dictionary<string, string> AutoModMessagesEntries = new()
    {
        ["Moderation_TabAutoMod"] = "AutoMod",
        ["Moderation_TabAutoModCount"] = "AutoMod ({0})",
        ["Moderation_AutoModNeedLogin"] = "Войдите с аккаунтом модератора (вкладка «Зрители»), чтобы проверять сообщения, задержанные AutoMod.",
        ["Moderation_AutoModEmpty"] = "AutoMod сейчас не задерживает сообщений.",
        ["Moderation_AutoModConnecting"] = "Подключение к AutoMod...",
        ["Moderation_AutoModUnavailable"] = "Проверка AutoMod недоступна. Выйдите и снова войдите в Twitch и убедитесь, что этот аккаунт — модератор канала.",
        ["Moderation_AutoModAllow"] = "Разрешить",
        ["Moderation_AutoModDeny"] = "Отклонить",
        ["Moderation_AutoModReasonGeneric"] = "Задержано AutoMod",
        ["Moderation_AutoModReasonBlockedTerm"] = "Задержано: содержит запрещённое слово",
        ["Moderation_AutoModReasonCategory"] = "Задержано AutoMod: {0}",
        ["Moderation_AutoModReasonCategoryLevel"] = "Задержано AutoMod: {0} (уровень {1})",
        ["Moderation_AutoModAllowed"] = "Сообщение от {0} разрешено.",
        ["Moderation_AutoModDenied"] = "Сообщение от {0} отклонено.",
        ["Moderation_AutoModAlreadyResolved"] = "Сообщение от {0} уже обработано.",
        ["Moderation_AutoModFailed"] = "Не удалось обновить сообщение от {0}.",
        ["Moderation_AutoModNeedsRelogin"] = "Чтобы проверять сообщения AutoMod, выйдите и снова войдите в Twitch (требуется новое разрешение).",
        ["EventMsg_AutoModHeld"] = "AutoMod задерживает сообщение от {0}.",
    };
}
