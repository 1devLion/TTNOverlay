namespace TTNOverlay.Twitch;

public enum AutoModHoldReason
{
    Unknown,

    /// <summary>AutoMod's own filters flagged the message (see <see cref="AutoModHeldMessage.Category"/>).</summary>
    AutoMod,

    /// <summary>The message matched a blocked term configured for the channel.</summary>
    BlockedTerm,
}

/// <summary>How a held message left the AutoMod queue.</summary>
public enum AutoModResolution
{
    Approved,
    Denied,

    /// <summary>Nobody reviewed it in time (Twitch drops held messages after a few hours).</summary>
    Expired,
}

/// <summary>A chat message that AutoMod is holding for a moderator to allow or deny.</summary>
public sealed class AutoModHeldMessage
{
    public string MessageId { get; init; } = "";
    public string UserId { get; init; } = "";
    public string Login { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Text { get; init; } = "";
    public AutoModHoldReason Reason { get; init; }

    /// <summary>AutoMod's category (for example "aggressive", "sexual_content"); null for blocked terms.</summary>
    public string? Category { get; init; }

    public int? Level { get; init; }
    public DateTime HeldAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>True while an allow/deny request for this message is in flight, so it can't be sent twice.</summary>
    public bool IsBusy { get; set; }
}

/// <summary>
/// Abstraction over the EventSub connection that reports messages held by AutoMod (automod.message.hold) and when
/// they stop being held (automod.message.update). Needs a moderator's user token with moderator:manage:automod.
/// </summary>
public interface ITwitchAutoModClient : IAsyncDisposable
{
    /// <summary>Raised on a background thread; marshal to the UI thread before touching UI state.</summary>
    event Action<AutoModHeldMessage>? MessageHeld;

    /// <summary>A held message was approved, denied or expired (by this app or by another moderator).</summary>
    event Action<string, AutoModResolution>? MessageResolved;

    event Action<EventSubStatus>? StatusChanged;

    /// <summary>
    /// Connects and keeps the session alive until disposed. The token provider is called on every (re)subscription
    /// so an expired access token is refreshed by the caller.
    /// </summary>
    void Start(string broadcasterUserId, string moderatorUserId, Func<Task<string?>> accessTokenProvider);
}
