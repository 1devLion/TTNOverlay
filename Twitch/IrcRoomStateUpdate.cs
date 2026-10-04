namespace TTNOverlay.Twitch;

/// <summary>
/// ROOMSTATE from Twitch IRC. On join it carries every field; afterwards it carries only the ones that changed, so
/// each field is null when the message did not mention it.
/// </summary>
/// <param name="EmoteOnly">emote-only: true when emote-only mode is on.</param>
/// <param name="FollowersOnlyMinutes">followers-only: -1 off, 0 on with no minimum follow time, otherwise the minimum minutes.</param>
/// <param name="UniqueChat">r9k: true when unique-chat mode is on.</param>
/// <param name="SlowSeconds">slow: seconds between messages, 0 when slow mode is off.</param>
/// <param name="SubsOnly">subs-only: true when subscribers-only mode is on.</param>
public readonly record struct IrcRoomStateUpdate(
    bool? EmoteOnly,
    int? FollowersOnlyMinutes,
    bool? UniqueChat,
    int? SlowSeconds,
    bool? SubsOnly
)
{
    public bool HasAny =>
        EmoteOnly.HasValue
        || FollowersOnlyMinutes.HasValue
        || UniqueChat.HasValue
        || SlowSeconds.HasValue
        || SubsOnly.HasValue;
}
