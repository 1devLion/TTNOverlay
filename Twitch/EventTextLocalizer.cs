using TTNOverlay.Models;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// Builds the localized display text for Twitch IRC system events (subs, raids, announcements, etc.).
///
/// All wording lives in the "EventMsg_*" keys in Services/Localization/&lt;Lang&gt;/EventMessages.cs
/// (see Strings.cs for how languages/fallback work). This class only picks which key(s) apply and
/// fills in the placeholders. Add a language by adding an EventMessages.cs there, not here.
/// </summary>
public static class EventTextLocalizer
{
    public static string? Build(
        string msgId,
        Dictionary<string, string> tags,
        string displayName,
        string? userMessage
    )
    {
        var lang = LocalizationService.Instance.CurrentLanguage;
        var eventKind = EventTypeIds.ParseTwitchMsgId(msgId);

        // Switches on the canonical EventType, not the raw msg-id string. See EventTypeIds.Classify.
        // EventType.Unknown (any msg-id Twitch sends that isn't mapped there) falls through to null
        // below, same as before: the caller (TwitchIrcClient) then falls back to Twitch's own
        // system-msg text, so an unrecognized event still gets a readable message, never silently
        // dropped.
        string? text = eventKind switch
        {
            EventType.Sub => BuildSub(tags, lang),
            EventType.Resub => BuildResub(tags, lang),
            EventType.SubGift => BuildSubgift(tags, lang),
            EventType.AnonSubGift => BuildAnonSubgift(tags, lang),
            EventType.MysteryGiftSub => BuildMysteryGift(tags, lang),
            EventType.AnonMysteryGiftSub => BuildAnonMysteryGift(tags, lang),
            EventType.Raid => BuildRaid(tags, lang),
            EventType.Ritual => BuildRitual(tags, lang),
            EventType.BitsBadgeTier => BuildBitsBadge(tags, lang),
            EventType.PrimeUpgrade => BuildPrimeUpgrade(tags, lang),
            EventType.GiftUpgrade => BuildGiftUpgrade(tags, lang),
            EventType.AnonGiftUpgrade => BuildAnonGiftUpgrade(lang),
            EventType.WatchStreak => BuildWatchStreak(tags, lang),
            EventType.BonusGift => BuildBonusGift(tags, lang),
            EventType.ViewerMilestone => BuildViewerMilestone(tags, lang),
            _ => null,
        };

        if (text is null)
            return null;

        if (!string.IsNullOrEmpty(userMessage))
            text += $"\n\"{userMessage}\"";

        return text;
    }

    private static string PlanName(string? subPlan, AppLanguage lang) =>
        subPlan switch
        {
            "Prime" => Strings.Get("EventMsg_PlanPrime", lang),
            "2000" => Strings.Get("EventMsg_PlanTier2", lang),
            "3000" => Strings.Get("EventMsg_PlanTier3", lang),
            _ => Strings.Get("EventMsg_PlanTier1", lang),
        };

    private static string BuildSub(Dictionary<string, string> tags, AppLanguage lang)
    {
        var plan = PlanName(tags.GetValueOrDefault("msg-param-sub-plan"), lang);
        return string.Format(Strings.Get("EventMsg_Sub", lang), plan);
    }

    private static string BuildResub(Dictionary<string, string> tags, AppLanguage lang)
    {
        var plan = PlanName(tags.GetValueOrDefault("msg-param-sub-plan"), lang);
        var monthsStr = tags.GetValueOrDefault("msg-param-cumulative-months", "1");
        var streakStr = tags.GetValueOrDefault("msg-param-streak-months", "0");
        var shareStreak = tags.GetValueOrDefault("msg-param-should-share-streak") == "1";

        var months = int.TryParse(monthsStr, out var m) ? m : 1;
        var streak = int.TryParse(streakStr, out var s) ? s : 0;

        var head = string.Format(Strings.Get("EventMsg_ResubHead", lang), plan);
        var totalPart = Strings.GetPlural("EventMsg_ResubTotal", months, lang, months);

        var streakPart = "";
        if (shareStreak && streakStr != "0" && streakStr != monthsStr)
            streakPart = Strings.GetPlural("EventMsg_ResubStreak", streak, lang, streak);

        return $"{head} {totalPart}{streakPart}";
    }

    private static string BuildSubgift(Dictionary<string, string> tags, AppLanguage lang)
    {
        var plan = PlanName(tags.GetValueOrDefault("msg-param-sub-plan"), lang);
        var recipient = tags.GetValueOrDefault("msg-param-recipient-display-name", Strings.Get("SlMsg_SomeoneLower", lang));
        var giftMonthsStr = tags.GetValueOrDefault("msg-param-gift-months", "1");
        var monthsPart = giftMonthsStr != "1"
            ? string.Format(Strings.Get("EventMsg_SubgiftMonths", lang), giftMonthsStr)
            : "";

        return string.Format(Strings.Get("EventMsg_Subgift", lang), plan, recipient) + monthsPart;
    }

    private static string BuildAnonSubgift(Dictionary<string, string> tags, AppLanguage lang)
    {
        var plan = PlanName(tags.GetValueOrDefault("msg-param-sub-plan"), lang);
        var recipient = tags.GetValueOrDefault("msg-param-recipient-display-name", Strings.Get("SlMsg_SomeoneLower", lang));
        return string.Format(Strings.Get("EventMsg_AnonSubgift", lang), plan, recipient);
    }

    private static string BuildMysteryGift(Dictionary<string, string> tags, AppLanguage lang)
    {
        var countStr = tags.GetValueOrDefault("msg-param-mass-gift-count", "1");
        var count = int.TryParse(countStr, out var c) ? c : 1;
        var total = tags.GetValueOrDefault("msg-param-sender-count");
        var sponsor = tags.GetValueOrDefault("msg-param-sponsor-name");

        var result = Strings.GetPlural("EventMsg_MysteryGift", count, lang, count);

        if (!string.IsNullOrEmpty(total))
            result += string.Format(Strings.Get("EventMsg_MysteryGiftTotal", lang), total);

        if (!string.IsNullOrEmpty(sponsor))
            result += string.Format(Strings.Get("EventMsg_MysteryGiftSponsor", lang), sponsor);

        return result + ".";
    }

    private static string BuildAnonMysteryGift(Dictionary<string, string> tags, AppLanguage lang)
    {
        var countStr = tags.GetValueOrDefault("msg-param-mass-gift-count", "1");
        var count = int.TryParse(countStr, out var c) ? c : 1;
        var sponsor = tags.GetValueOrDefault("msg-param-sponsor-name");

        var result = Strings.GetPlural("EventMsg_AnonMysteryGift", count, lang, count);

        if (!string.IsNullOrEmpty(sponsor))
            result += string.Format(Strings.Get("EventMsg_MysteryGiftSponsor", lang), sponsor);

        return result + ".";
    }

    private static string BuildRaid(Dictionary<string, string> tags, AppLanguage lang)
    {
        var viewersStr = tags.GetValueOrDefault("msg-param-viewerCount", "0");
        var viewers = int.TryParse(viewersStr, out var v) ? v : 0;
        return Strings.GetPlural("EventMsg_Raid", viewers, lang, viewers);
    }

    private static string BuildRitual(Dictionary<string, string> tags, AppLanguage lang) =>
        tags.GetValueOrDefault("msg-param-ritual-name") == "new_chatter"
            ? Strings.Get("EventMsg_RitualNewChatter", lang)
            : Strings.Get("EventMsg_RitualOther", lang);

    private static string BuildBitsBadge(Dictionary<string, string> tags, AppLanguage lang)
    {
        var threshold = tags.GetValueOrDefault("msg-param-threshold", "0");
        return string.Format(Strings.Get("EventMsg_BitsBadge", lang), threshold);
    }

    private static string BuildPrimeUpgrade(Dictionary<string, string> tags, AppLanguage lang)
    {
        var plan = PlanName(tags.GetValueOrDefault("msg-param-sub-plan"), lang);
        return string.Format(Strings.Get("EventMsg_PrimeUpgrade", lang), plan);
    }

    private static string BuildGiftUpgrade(Dictionary<string, string> tags, AppLanguage lang)
    {
        var gifter =
            tags.GetValueOrDefault("msg-param-sender-display-name")
            ?? tags.GetValueOrDefault("msg-param-sender-name", Strings.Get("SlMsg_SomeoneLower", lang));
        return string.Format(Strings.Get("EventMsg_GiftUpgrade", lang), gifter);
    }

    private static string BuildAnonGiftUpgrade(AppLanguage lang) =>
        Strings.Get("EventMsg_AnonGiftUpgrade", lang);

    private static string BuildWatchStreak(Dictionary<string, string> tags, AppLanguage lang)
    {
        var countStr = tags.GetValueOrDefault("msg-param-count");
        if (string.IsNullOrEmpty(countStr))
            countStr = tags.GetValueOrDefault("msg-param-watch-streak-count", "?");

        var count = int.TryParse(countStr, out var c) ? c : 2;
        return Strings.GetPlural("EventMsg_WatchStreak", count, lang, countStr);
    }

    private static string BuildBonusGift(Dictionary<string, string> tags, AppLanguage lang)
    {
        var countStr = tags.GetValueOrDefault("msg-param-count", "?");
        var count = int.TryParse(countStr, out var c) ? c : 2;
        var sponsor = tags.GetValueOrDefault("msg-param-sponsor-name", Strings.Get("EventMsg_UnknownSponsor", lang));

        return Strings.GetPlural("EventMsg_BonusGift", count, lang, countStr, sponsor);
    }

    private static string BuildViewerMilestone(Dictionary<string, string> tags, AppLanguage lang)
    {
        var category = tags.GetValueOrDefault("msg-param-category", "");
        var valueStr = tags.GetValueOrDefault("msg-param-value", "?");

        if (category == "watch-streak")
        {
            var value = int.TryParse(valueStr, out var v) ? v : 2;
            return Strings.GetPlural("EventMsg_WatchStreak", value, lang, valueStr);
        }

        return string.Format(Strings.Get("EventMsg_ViewerMilestone", lang), category, valueStr);
    }
}