using TTNOverlay.Models;

namespace TTNOverlay.Twitch;

public enum EventSubStatus
{
    Connecting,

    /// <summary>The redemption subscription was accepted; events are flowing.</summary>
    Subscribed,

    /// <summary>Connection lost or failed; the client is retrying with backoff on its own.</summary>
    Disconnected,

    /// <summary>Twitch refused the subscription (missing scope or invalid token). Needs a new Twitch login; no retries.</summary>
    NeedsRelogin,
}

/// <summary>
/// Abstraction over the Twitch EventSub WebSocket client used for channel-points redemptions.
/// Requires the broadcaster's own user access token with the channel:read:redemptions scope.
/// </summary>
public interface ITwitchEventSubClient : IAsyncDisposable
{
    /// <summary>Raised on a background thread; marshal to the UI thread before touching UI state.</summary>
    event Action<ChannelPointsRedemption>? RedemptionReceived;

    event Action<EventSubStatus>? StatusChanged;

    /// <summary>
    /// Connects and keeps the session alive (reconnects with backoff) until disposed. The token provider is
    /// called on every (re)subscription so an expired access token is refreshed by the caller.
    /// </summary>
    void Start(string broadcasterUserId, Func<Task<string?>> accessTokenProvider);
}
