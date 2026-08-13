namespace TTNOverlay.Services;

internal static partial class EnStrings
{
    private static readonly Dictionary<string, string> EventMessagesEntries = new()
    {
        ["EventMsg_PlanPrime"] = "Prime",
        ["EventMsg_PlanTier1"] = "Tier 1",
        ["EventMsg_PlanTier2"] = "Tier 2",
        ["EventMsg_PlanTier3"] = "Tier 3",
        ["EventMsg_Sub"] = "Subscribed with {0}.",
        ["EventMsg_ResubHead"] = "Resubscribed with {0}.",
        ["EventMsg_Subgift"] = "Gifted a {0} subscription to {1}.",
        ["EventMsg_SubgiftMonths"] = " ({0} months gifted)",
        ["EventMsg_AnonSubgift"] = "An anonymous viewer gifted a {0} subscription to {1}.",
        ["EventMsg_MysteryGiftTotal"] = " (has gifted {0} in the channel)",
        ["EventMsg_MysteryGiftSponsor"] = ", sponsored by {0}",
        ["EventMsg_RitualNewChatter"] = "Chatted for the first time.",
        ["EventMsg_RitualOther"] = "Took part in a chat event.",
        ["EventMsg_BitsBadge"] = "Reached the {0} bits badge.",
        ["EventMsg_PrimeUpgrade"] = "Upgraded from Prime to a paid subscription ({0}).",
        ["EventMsg_GiftUpgrade"] = "Continued paying for the subscription gifted by {0}.",
        ["EventMsg_AnonGiftUpgrade"] = "Continued paying for the subscription gifted by an anonymous viewer.",
        ["EventMsg_UnknownSponsor"] = "an event",
        ["EventMsg_ViewerMilestone"] = "Reached a milestone: {0} ({1}).",
        ["EventMsg_ResubStreak_One"] = " Streak of {0} month in a row!",
        ["EventMsg_ResubStreak_Other"] = " Streak of {0} months in a row!",
        ["EventMsg_ResubTotal_One"] = "Been subscribed for {0} month in total.",
        ["EventMsg_ResubTotal_Other"] = "Been subscribed for {0} months in total.",
        ["EventMsg_MysteryGift_One"] = "Gifted {0} mystery subscription to the channel",
        ["EventMsg_MysteryGift_Other"] = "Gifted {0} mystery subscriptions to the channel",
        ["EventMsg_AnonMysteryGift_One"] = "An anonymous viewer gifted {0} mystery subscription to the channel",
        ["EventMsg_AnonMysteryGift_Other"] = "An anonymous viewer gifted {0} mystery subscriptions to the channel",
        ["EventMsg_Raid_One"] = "Raided with {0} viewer.",
        ["EventMsg_Raid_Other"] = "Raided with {0} viewers.",
        ["EventMsg_WatchStreak_One"] = "Reached a streak of {0} stream in a row!",
        ["EventMsg_WatchStreak_Other"] = "Reached a streak of {0} streams in a row!",
        ["EventMsg_BonusGift_One"] = "Received {0} extra subscription sponsored by {1}!",
        ["EventMsg_BonusGift_Other"] = "Received {0} extra subscriptions sponsored by {1}!",
    };
}
