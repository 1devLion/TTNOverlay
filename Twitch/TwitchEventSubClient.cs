using System.Text.Json;
using TTNOverlay.Models;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// Twitch EventSub for channel-points redemptions (custom and automatic rewards). IRC can't do this job: it never
/// carries the reward title/cost, and redemptions without a message don't appear in chat at all. Requires the
/// broadcaster's own user token (channel:read:redemptions). The WebSocket plumbing lives in
/// <see cref="EventSubSessionClient"/>.
/// </summary>
public sealed class TwitchEventSubClient : EventSubSessionClient, ITwitchEventSubClient
{
    private const string CustomRedemptionType = "channel.channel_points_custom_reward_redemption.add";
    private const string AutoRedemptionType = "channel.channel_points_automatic_reward_redemption.add";

    public event Action<ChannelPointsRedemption>? RedemptionReceived;

    private string _broadcasterId = "";

    protected override string LogPrefix => "TwitchEventSubClient";

    public void Start(string broadcasterUserId, Func<Task<string?>> accessTokenProvider)
    {
        _broadcasterId = broadcasterUserId;
        StartSession(accessTokenProvider);
    }

    protected override async Task<SubscribeOutcome> SubscribeAsync(
        string sessionId,
        string accessToken,
        CancellationToken ct
    )
    {
        var condition = new Dictionary<string, string> { ["broadcaster_user_id"] = _broadcasterId };

        var custom = await CreateSubscriptionAsync(CustomRedemptionType, "1", condition, sessionId, accessToken, ct);
        if (custom != SubscribeOutcome.Ok)
            return custom;

        // Built-in rewards (highlight my message, unlock an emote, ...). Optional: custom rewards keep working without it.
        var auto = await CreateSubscriptionAsync(AutoRedemptionType, "1", condition, sessionId, accessToken, ct);
        if (auto != SubscribeOutcome.Ok)
            DebugLog.Write($"TwitchEventSubClient: automatic reward redemptions unavailable ({auto})");

        return SubscribeOutcome.Ok;
    }

    protected override void OnNotification(string? subscriptionType, JsonElement ev)
    {
        ChannelPointsRedemption? redemption = subscriptionType switch
        {
            CustomRedemptionType => ParseCustomRedemption(ev),
            AutoRedemptionType => ParseAutoRedemption(ev),
            _ => null,
        };

        if (redemption is not null)
            RedemptionReceived?.Invoke(redemption);
    }

    private static ChannelPointsRedemption ParseCustomRedemption(JsonElement ev)
    {
        TryGetObject(ev, "reward", out var reward);

        return new ChannelPointsRedemption
        {
            RedemptionId = GetString(ev, "id") ?? "",
            RewardId = GetString(reward, "id"),
            UserLogin = GetString(ev, "user_login") ?? "",
            DisplayName = GetString(ev, "user_name") ?? GetString(ev, "user_login") ?? "",
            RewardTitle = GetString(reward, "title"),
            Cost = GetInt(reward, "cost"),
            UserInput = GetString(ev, "user_input"),
        };
    }

    private static ChannelPointsRedemption ParseAutoRedemption(JsonElement ev)
    {
        TryGetObject(ev, "reward", out var reward);
        TryGetObject(ev, "message", out var message);

        return new ChannelPointsRedemption
        {
            RedemptionId = GetString(ev, "id") ?? "",
            UserLogin = GetString(ev, "user_login") ?? "",
            DisplayName = GetString(ev, "user_name") ?? GetString(ev, "user_login") ?? "",
            AutoRewardType = GetString(reward, "type"),
            // Rewards paid in bits (e.g. gigantify_an_emote) report 0; treated as "no cost" by the message builder.
            Cost = GetInt(reward, "cost") ?? GetInt(reward, "channel_points"),
            UserInput = GetString(ev, "user_input") ?? GetString(message, "text"),
        };
    }
}
