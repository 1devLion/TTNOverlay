namespace TTNOverlay.Models;

/// <summary>
/// Plain data models for a rendered chat entry: badges, emote positions, and the chat message itself (including optional event/announcement/sub fields).
/// </summary>
public class Badge
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
}

public enum EmoteSource
{
    Twitch,
    Bttv,
    Ffz,
    SevenTv,
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
    public DateTime ReceivedAt { get; set; } = DateTime.Now;
    public bool IsAction { get; set; }
    public bool IsSystem { get; set; }

    /// <summary>
    /// True for messages that should never be swept by the MessageTimeoutSeconds timer (e.g. the
    /// first-run welcome guide) -- they still fall off the top once MaxMessages is exceeded by real
    /// chat activity, they just don't vanish on a timer while nothing else is coming in. Regular
    /// IsSystem messages (Twitch NOTICE/USERNOTICE, Streamlabs events) are NOT persistent -- they're
    /// meant to expire like any other chat line.
    /// </summary>
    public bool IsPersistent { get; set; }

    /// <summary>
    /// True for the welcome-guide system message that should render a "Log in with Twitch" button
    /// beneath its text (see SeedWelcomeGuide / DrawWelcomeTwitchLoginButton). The button hides itself
    /// once the user is logged in (see DrawMessage), so this stays true even after login.
    /// </summary>
    public bool IsTwitchLoginPrompt { get; set; }

    public string? EventType { get; set; }

    public string? AnnouncementColor { get; set; }

    public int? StreakMonths { get; set; }

    public string? SubPlanName { get; set; }

    public string? EventImageUrl { get; set; }
}