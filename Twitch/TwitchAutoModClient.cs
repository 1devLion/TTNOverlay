using System.Globalization;
using System.Text.Json;
using TTNOverlay.Services;

namespace TTNOverlay.Twitch;

/// <summary>
/// Twitch EventSub for AutoMod: which messages are being held for review, and when that stops. IRC never carries
/// held messages (they are not published to chat until a moderator allows them), so EventSub is the only source.
/// Subscriptions use the moderator's own token and need the moderator:manage:automod scope. Version 2 is tried
/// first and version 1 is the fallback, since both report the same facts the panel needs.
/// </summary>
public sealed class TwitchAutoModClient : EventSubSessionClient, ITwitchAutoModClient
{
    private const string HoldType = "automod.message.hold";
    private const string UpdateType = "automod.message.update";

    public event Action<AutoModHeldMessage>? MessageHeld;
    public event Action<string, AutoModResolution>? MessageResolved;

    private string _broadcasterId = "";
    private string _moderatorId = "";

    protected override string LogPrefix => "TwitchAutoModClient";

    public void Start(string broadcasterUserId, string moderatorUserId, Func<Task<string?>> accessTokenProvider)
    {
        _broadcasterId = broadcasterUserId;
        _moderatorId = moderatorUserId;
        StartSession(accessTokenProvider);
    }

    protected override async Task<SubscribeOutcome> SubscribeAsync(
        string sessionId,
        string accessToken,
        CancellationToken ct
    )
    {
        var condition = new Dictionary<string, string>
        {
            ["broadcaster_user_id"] = _broadcasterId,
            ["moderator_user_id"] = _moderatorId,
        };

        string version = "2";
        var hold = await CreateSubscriptionAsync(HoldType, version, condition, sessionId, accessToken, ct);
        if (hold == SubscribeOutcome.Rejected)
        {
            version = "1";
            hold = await CreateSubscriptionAsync(HoldType, version, condition, sessionId, accessToken, ct);
        }
        if (hold != SubscribeOutcome.Ok)
            return hold;

        // Optional: without it a message another moderator resolves stays listed until someone tries to act on it,
        // and Twitch then answers that it is already handled.
        var update = await CreateSubscriptionAsync(UpdateType, version, condition, sessionId, accessToken, ct);
        if (update != SubscribeOutcome.Ok)
            DebugLog.Write($"TwitchAutoModClient: automod.message.update unavailable ({update})");

        return SubscribeOutcome.Ok;
    }

    protected override void OnNotification(string? subscriptionType, JsonElement ev)
    {
        switch (subscriptionType)
        {
            case HoldType:
                if (ParseHeld(ev) is { } held)
                    MessageHeld?.Invoke(held);
                break;

            case UpdateType:
                var id = GetString(ev, "message_id");
                if (!string.IsNullOrEmpty(id) && ParseResolution(GetString(ev, "status")) is { } resolution)
                    MessageResolved?.Invoke(id, resolution);
                break;
        }
    }

    internal static AutoModHeldMessage? ParseHeld(JsonElement ev)
    {
        var messageId = GetString(ev, "message_id");
        if (string.IsNullOrEmpty(messageId))
            return null;

        // v1: "message" is the text. v2: "message" is an object with "text" and "fragments".
        string text =
            ev.TryGetProperty("message", out var message)
                ? message.ValueKind switch
                {
                    JsonValueKind.String => message.GetString() ?? "",
                    JsonValueKind.Object => GetString(message, "text") ?? "",
                    _ => "",
                }
                : "";

        var reason = AutoModHoldReason.Unknown;
        string? category = GetString(ev, "category");
        int? level = GetInt(ev, "level");

        switch (GetString(ev, "reason"))
        {
            case "blocked_term":
                reason = AutoModHoldReason.BlockedTerm;
                break;
            case "automod":
                reason = AutoModHoldReason.AutoMod;
                break;
        }

        if (TryGetObject(ev, "automod", out var automod))
        {
            category ??= GetString(automod, "category");
            level ??= GetInt(automod, "level");
            if (reason == AutoModHoldReason.Unknown)
                reason = AutoModHoldReason.AutoMod;
        }
        else if (reason == AutoModHoldReason.Unknown && category is not null)
        {
            reason = AutoModHoldReason.AutoMod;
        }

        var login = GetString(ev, "user_login") ?? "";

        return new AutoModHeldMessage
        {
            MessageId = messageId,
            UserId = GetString(ev, "user_id") ?? "",
            Login = login,
            DisplayName = GetString(ev, "user_name") ?? login,
            Text = text,
            Reason = reason,
            Category = category,
            Level = level,
            HeldAtUtc = ParseTime(GetString(ev, "held_at")),
        };
    }

    internal static AutoModResolution? ParseResolution(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "approved" => AutoModResolution.Approved,
            "denied" => AutoModResolution.Denied,
            "expired" => AutoModResolution.Expired,
            _ => null,
        };

    private static DateTime ParseTime(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed
        )
            ? parsed.UtcDateTime
            : DateTime.UtcNow;
}
