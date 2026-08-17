namespace TTNOverlay.Models;

/// <summary>
/// Plain data models for a rendered chat entry: badges, emote positions, and the chat message itself (including optional event/announcement/sub fields).
/// </summary>
public class Badge
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";

    /// <summary>
    /// Direct image URL for this badge instance, when the source platform hands one back inline
    /// (e.g. Kick's per-channel subscriber_badges, resolved by tenure months). Null for badges that
    /// rely on the existing Name/Version lookup against a pre-fetched map (Twitch badges via
    /// _badgeUrls). When set, DrawBadges uses this directly and skips the map lookup.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Embedded resource key (see KickBadgeIconLoader) for badges with no Kick-hosted image URL.
    /// Kick's fixed global role badges (moderator/vip/og/founder/broadcaster/verified/staff) and
    /// sub_gifter tiers, extracted as WebP and shipped inside the app. Checked by DrawBadges only
    /// when both IconUrl and the _badgeUrls map lookup come up empty.
    /// </summary>
    public string? LocalIcon { get; set; }
}

public enum EmoteSource
{
    Twitch,
    Bttv,
    Ffz,
    SevenTv,
    Kick,
}

public class EmotePosition
{
    public string Id { get; set; } = "";
    public int Start { get; set; }
    public int End { get; set; }

    public EmoteSource Source { get; set; } = EmoteSource.Twitch;
    public string? StaticUrl { get; set; }
    public string? AnimatedUrl { get; set; }
}

public class ChatMessage
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Color { get; set; } = "#B0B0B0";
    public string Text { get; set; } = "";
    public List<Badge> Badges { get; set; } = new();
    public List<EmotePosition> Emotes { get; set; } = new();
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public bool IsAction { get; set; }
    public bool IsSystem { get; set; }

    /// <summary>
    /// True for messages that should never be swept by the MessageTimeoutSeconds timer.
    /// </summary>
    public bool IsPersistent { get; set; }

    /// <summary>
    /// True for the welcome-guide system message that should render a "Log in with Twitch" button
    /// beneath its text.
    /// </summary>
    public bool IsTwitchLoginPrompt { get; set; }

    public string? EventType { get; set; }

    /// <summary>
    /// Which service this event came from, derived from <see cref="EventType"/> via
    /// <see cref="EventTypeIds.Classify"/>. Null for ordinary chat messages (EventType is null).
    /// </summary>
    public Platform? Platform { get; set; }

    /// <summary>
    /// Canonical classification of <see cref="EventType"/>, derived via <see cref="EventTypeIds.Classify"/>.
    /// Defaults to Unknown (the same "generic event" appearance as before this field existed). It is
    /// a convenience for switch-based icon/color/family logic, never a replacement for EventType, which
    /// remains the raw string that is actually persisted and always carries whatever Twitch/Streamlabs sent.
    /// </summary>
    public EventType EventKind { get; set; } = global::TTNOverlay.Models.EventType.Unknown;

    public string? AnnouncementColor { get; set; }

    public int? StreakMonths { get; set; }

    public string? SubPlanName { get; set; }

    public string? EventImageUrl { get; set; }
}