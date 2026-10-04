namespace TTNOverlay.Services;

internal static partial class EnStrings
{
    private static readonly Dictionary<string, string> AutoModMessagesEntries = new()
    {
        ["Moderation_TabAutoMod"] = "AutoMod",
        ["Moderation_TabAutoModCount"] = "AutoMod ({0})",
        ["Moderation_AutoModNeedLogin"] = "Log in with a moderator account (Chatters tab) to review messages held by AutoMod.",
        ["Moderation_AutoModEmpty"] = "AutoMod is not holding any messages.",
        ["Moderation_AutoModConnecting"] = "Connecting to AutoMod...",
        ["Moderation_AutoModUnavailable"] = "AutoMod review is unavailable. Log out and log in with Twitch again, and make sure this account is a moderator of the channel.",
        ["Moderation_AutoModAllow"] = "Allow",
        ["Moderation_AutoModDeny"] = "Deny",
        ["Moderation_AutoModReasonGeneric"] = "Held by AutoMod",
        ["Moderation_AutoModReasonBlockedTerm"] = "Held: contains a blocked term",
        ["Moderation_AutoModReasonCategory"] = "Held by AutoMod: {0}",
        ["Moderation_AutoModReasonCategoryLevel"] = "Held by AutoMod: {0} (level {1})",
        ["Moderation_AutoModAllowed"] = "Allowed the message from {0}.",
        ["Moderation_AutoModDenied"] = "Denied the message from {0}.",
        ["Moderation_AutoModAlreadyResolved"] = "The message from {0} was already handled.",
        ["Moderation_AutoModFailed"] = "Could not update the message from {0}.",
        ["Moderation_AutoModNeedsRelogin"] = "To review AutoMod messages, log out and log in with Twitch again (a new permission is required).",
        ["EventMsg_AutoModHeld"] = "AutoMod is holding a message from {0}.",
    };
}
