namespace TTNOverlay.Models;

/// <summary>
/// Platform-agnostic classification of chat events (subscriptions, raids, donations, etc.).
/// </summary>
public enum EventType
{
    /// <summary>
    /// Event type could not be classified or is not yet supported.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A new subscription.
    /// </summary>
    Sub,

    /// <summary>
    /// A subscription renewal.
    /// </summary>
    Resub,

    /// <summary>
    /// A gifted subscription to another user.
    /// </summary>
    SubGift,

    /// <summary>
    /// An anonymous gifted subscription.
    /// </summary>
    AnonSubGift,

    /// <summary>
    /// A mystery gift subscription (number of gifts not specified).
    /// </summary>
    MysteryGiftSub,

    /// <summary>
    /// An anonymous mystery gift subscription.
    /// </summary>
    AnonMysteryGiftSub,

    /// <summary>
    /// Upgrade from a Prime subscription.
    /// </summary>
    PrimeUpgrade,

    /// <summary>
    /// Upgrade from a gifted subscription.
    /// </summary>
    GiftUpgrade,

    /// <summary>
    /// Anonymous upgrade from a gifted subscription.
    /// </summary>
    AnonGiftUpgrade,

    /// <summary>
    /// A raid event.
    /// </summary>
    Raid,

    /// <summary>
    /// Bits (cheers) event.
    /// </summary>
    Bits,

    /// <summary>
    /// A donation (external or platform-based).
    /// </summary>
    Donation,

    /// <summary>
    /// A follow event.
    /// </summary>
    Follow,

    /// <summary>
    /// A host event.
    /// </summary>
    Host,

    /// <summary>
    /// A merchandise purchase event.
    /// </summary>
    Merch,

    /// <summary>
    /// A power-up event.
    /// </summary>
    PowerUp,

    /// <summary>
    /// A ritual event (e.g., new viewer, etc.).
    /// </summary>
    Ritual,

    /// <summary>
    /// Bits badge tier upgrade event.
    /// </summary>
    BitsBadgeTier,

    /// <summary>
    /// An announcement event.
    /// </summary>
    Announcement,

    /// <summary>
    /// A watch streak milestone event.
    /// </summary>
    WatchStreak,

    /// <summary>
    /// A bonus gift event.
    /// </summary>
    BonusGift,

    /// <summary>
    /// A viewer milestone event.
    /// </summary>
    ViewerMilestone,
}