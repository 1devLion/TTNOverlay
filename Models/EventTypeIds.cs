namespace TTNOverlay.Models;

/// <summary>
/// Provides raw event identifier constants for Twitch and Streamlabs events,
/// along with classification and parsing methods.
/// </summary>
public static class EventTypeIds
{
    /// <summary>
    /// Raw Twitch IRC msg-id values as sent in USERNOTICE tags.
    /// </summary>
    public static class Twitch
    {
        public const string Sub = "sub";
        public const string Resub = "resub";
        public const string SubGift = "subgift";
        public const string AnonSubGift = "anonsubgift";
        public const string SubMysteryGift = "submysterygift";
        public const string AnonSubMysteryGift = "anonsubmysterygift";
        public const string PrimePaidUpgrade = "primepaidupgrade";
        public const string GiftPaidUpgrade = "giftpaidupgrade";
        public const string AnonGiftPaidUpgrade = "anongiftpaidupgrade";
        public const string Raid = "raid";
        public const string Ritual = "ritual";
        public const string BitsBadgeTier = "bitsbadgetier";
        public const string Announcement = "announcement";
        public const string WatchStreak = "watchstreak";
        public const string Bonus = "bonus";
        public const string BonusGift = "bonusgift";
        public const string ViewerMilestone = "viewermilestone";
    }

    /// <summary>
    /// Raw event identifiers for Streamlabs events, all prefixed with "sl_".
    /// </summary>
    public static class Streamlabs
    {
        public const string Prefix = "sl_";

        public const string Donation = "sl_donation";
        public const string Follow = "sl_follow";
        public const string Host = "sl_host";
        public const string Merch = "sl_merch";
        public const string Subscription = "sl_subscription";
        public const string Bits = "sl_bits";
        public const string PowerUp = "sl_powerup";
        public const string Raid = "sl_raid";
        public const string SubGift = "sl_subgift";
        public const string AnonSubGift = "sl_anonsubgift";
        public const string SubMysteryGift = "sl_submysterygift";
        public const string AnonMysteryGift = "sl_anonmysterygift";
    }

    /// <summary>
    /// Classifies a raw event ID into its platform and canonical event type.
    /// </summary>
    /// <param name="rawEventId">The raw event ID string.</param>
    /// <returns>
    /// A tuple containing the platform and the corresponding <see cref="EventType"/>.
    /// Returns (null, Unknown) for null or empty input.
    /// </returns>
    public static (Platform? Platform, EventType Kind) Classify(string? rawEventId)
    {
        if (string.IsNullOrEmpty(rawEventId))
            return (null, EventType.Unknown);

        if (rawEventId.StartsWith(Streamlabs.Prefix, StringComparison.Ordinal))
            return (Platform.Streamlabs, ParseStreamlabsId(rawEventId));

        return (Platform.Twitch, ParseTwitchMsgId(rawEventId));
    }

    /// <summary>
    /// Maps a Twitch msg-id to its corresponding <see cref="EventType"/>.
    /// </summary>
    /// <param name="msgId">The Twitch msg-id.</param>
    /// <returns>The mapped event type, or Unknown if not recognized.</returns>
    public static EventType ParseTwitchMsgId(string? msgId) =>
        msgId switch
        {
            Twitch.Sub => EventType.Sub,
            Twitch.Resub => EventType.Resub,
            Twitch.SubGift => EventType.SubGift,
            Twitch.AnonSubGift => EventType.AnonSubGift,
            Twitch.SubMysteryGift => EventType.MysteryGiftSub,
            Twitch.AnonSubMysteryGift => EventType.AnonMysteryGiftSub,
            Twitch.PrimePaidUpgrade => EventType.PrimeUpgrade,
            Twitch.GiftPaidUpgrade => EventType.GiftUpgrade,
            Twitch.AnonGiftPaidUpgrade => EventType.AnonGiftUpgrade,
            Twitch.Raid => EventType.Raid,
            Twitch.Ritual => EventType.Ritual,
            Twitch.BitsBadgeTier => EventType.BitsBadgeTier,
            Twitch.Announcement => EventType.Announcement,
            Twitch.WatchStreak => EventType.WatchStreak,
            Twitch.Bonus => EventType.BonusGift,
            Twitch.BonusGift => EventType.BonusGift,
            Twitch.ViewerMilestone => EventType.ViewerMilestone,
            _ => EventType.Unknown,
        };

    /// <summary>
    /// Maps a Streamlabs event ID to its corresponding <see cref="EventType"/>.
    /// </summary>
    /// <param name="id">The Streamlabs event ID (with "sl_" prefix).</param>
    /// <returns>The mapped event type, or Unknown if not recognized.</returns>
    public static EventType ParseStreamlabsId(string? id) =>
        id switch
        {
            Streamlabs.Donation => EventType.Donation,
            Streamlabs.Follow => EventType.Follow,
            Streamlabs.Host => EventType.Host,
            Streamlabs.Merch => EventType.Merch,
            Streamlabs.Subscription => EventType.Sub,
            Streamlabs.Bits => EventType.Bits,
            Streamlabs.PowerUp => EventType.PowerUp,
            Streamlabs.Raid => EventType.Raid,
            Streamlabs.SubGift => EventType.SubGift,
            Streamlabs.AnonSubGift => EventType.AnonSubGift,
            Streamlabs.SubMysteryGift => EventType.MysteryGiftSub,
            Streamlabs.AnonMysteryGift => EventType.AnonMysteryGiftSub,
            "sl_cheer" => EventType.Bits,
            _ => EventType.Unknown,
        };

    /// <summary>
    /// Normalizes Streamlabs raw event type strings to a canonical form.
    /// </summary>
    /// <param name="type">The raw type string.</param>
    /// <returns>The normalized type string, or the original if no mapping applies.</returns>
    public static string NormalizeStreamlabsRawType(string type) =>
        type switch
        {
            "twitchcharitydonation" => "donation",
            "cheer" => "bits",
            _ => type,
        };
}