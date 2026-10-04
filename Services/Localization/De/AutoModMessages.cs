namespace TTNOverlay.Services;

internal static partial class DeStrings
{
    private static readonly Dictionary<string, string> AutoModMessagesEntries = new()
    {
        ["Moderation_TabAutoMod"] = "AutoMod",
        ["Moderation_TabAutoModCount"] = "AutoMod ({0})",
        ["Moderation_AutoModNeedLogin"] = "Melden Sie sich mit einem Moderatorkonto an (Reiter Chatter), um von AutoMod zurückgehaltene Nachrichten zu prüfen.",
        ["Moderation_AutoModEmpty"] = "AutoMod hält derzeit keine Nachrichten zurück.",
        ["Moderation_AutoModConnecting"] = "Verbinde mit AutoMod...",
        ["Moderation_AutoModUnavailable"] = "Die AutoMod-Prüfung ist nicht verfügbar. Melden Sie sich ab und erneut bei Twitch an und stellen Sie sicher, dass dieses Konto Moderator des Kanals ist.",
        ["Moderation_AutoModAllow"] = "Zulassen",
        ["Moderation_AutoModDeny"] = "Ablehnen",
        ["Moderation_AutoModReasonGeneric"] = "Von AutoMod zurückgehalten",
        ["Moderation_AutoModReasonBlockedTerm"] = "Zurückgehalten: enthält einen gesperrten Begriff",
        ["Moderation_AutoModReasonCategory"] = "Von AutoMod zurückgehalten: {0}",
        ["Moderation_AutoModReasonCategoryLevel"] = "Von AutoMod zurückgehalten: {0} (Stufe {1})",
        ["Moderation_AutoModAllowed"] = "Nachricht von {0} zugelassen.",
        ["Moderation_AutoModDenied"] = "Nachricht von {0} abgelehnt.",
        ["Moderation_AutoModAlreadyResolved"] = "Die Nachricht von {0} wurde bereits bearbeitet.",
        ["Moderation_AutoModFailed"] = "Die Nachricht von {0} konnte nicht aktualisiert werden.",
        ["Moderation_AutoModNeedsRelogin"] = "Um AutoMod-Nachrichten zu prüfen, melden Sie sich ab und erneut bei Twitch an (eine neue Berechtigung ist erforderlich).",
        ["EventMsg_AutoModHeld"] = "AutoMod hält eine Nachricht von {0} zurück.",
    };
}
