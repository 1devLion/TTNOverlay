namespace TTNOverlay.Models;

/// <summary>
/// A viewer's Twitch channel-points redemption, normalized from either source: EventSub (full detail:
/// reward title, cost, user input) or the IRC fallback (only what the chat tags carry, so no title/cost).
/// </summary>
public sealed class ChannelPointsRedemption
{
    public string RedemptionId { get; init; } = "";

    /// <summary>Custom reward id (a GUID). Null for built-in "automatic" rewards.</summary>
    public string? RewardId { get; init; }

    public string UserLogin { get; init; } = "";
    public string DisplayName { get; init; } = "";

    /// <summary>Title of a custom reward, as set by the streamer. Null for automatic rewards and IRC fallback.</summary>
    public string? RewardTitle { get; init; }

    /// <summary>Points spent. Null when unknown (IRC fallback) or not meaningful (automatic rewards paid in bits report 0).</summary>
    public int? Cost { get; init; }

    /// <summary>
    /// EventSub's reward.type for built-in rewards (send_highlighted_message, gigantify_an_emote, ...).
    /// Null for custom rewards.
    /// </summary>
    public string? AutoRewardType { get; init; }

    /// <summary>The text the viewer typed when redeeming (custom reward input, or the message of an automatic reward).</summary>
    public string? UserInput { get; init; }
}
