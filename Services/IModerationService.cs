using TTNOverlay.Twitch;

namespace TTNOverlay.Services;

/// <summary>
/// The per-message moderation actions shared by every platform (Twitch's <see cref="IModerationService"/> and Kick's
/// KickModerationService), so <see cref="ModerationActionRunner"/> works the same on both.
/// </summary>
public interface IChatModerationActions
{
    /// <summary>
    /// Sends a warning to a chatter (Twitch only; platforms without warnings return false and the UI doesn't offer it).
    /// </summary>
    Task<bool> WarnAsync(string channelLogin, string targetUserId, string reason);

    Task<bool> TimeoutAsync(
        string channelLogin,
        string targetUserId,
        int durationSeconds,
        string? reason = null
    );

    Task<bool> BanAsync(string channelLogin, string targetUserId, string? reason = null);

    /// <summary>Deletes a single chat message (Twitch only allows messages from the last 6 hours, not the broadcaster's or other moderators').</summary>
    Task<ModerationDeleteResult> DeleteMessageAsync(string channelLogin, string messageId);

    /// <summary>Sends a chat message to the channel as the logged-in account.</summary>
    Task<ChatSendResult> SendChatMessageAsync(string channelLogin, string text);
}

/// <summary>
/// Abstraction over Twitch chat moderation actions (login state, chatters, timeouts/bans) used by the moderation panel.
/// </summary>
public interface IModerationService : IChatModerationActions
{
    bool IsLoggedIn { get; }
    string ModeratorLogin { get; }
    bool HasCredentials { get; }

    Task<bool> LoginAsync(CancellationToken cancellationToken = default);
    void Logout();

    /// <summary>
    /// Checks that the stored session carries every scope the app currently requires. If it doesn't, logs out and
    /// returns the missing scopes (the caller should ask the user to log in again); empty means all good, or that it
    /// couldn't be checked right now.
    /// </summary>
    Task<IReadOnlyList<string>> EnsureRequiredScopesAsync() => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    /// <summary>Returns a valid user access token (refreshing it if needed), or null if not logged in.</summary>
    Task<string?> GetAccessTokenAsync();

    Task<List<(string Id, string Login)>?> GetChattersAsync(string channelLogin);

    Task<List<(
        string Id,
        string Login,
        DateTime? ExpiresAt,
        string Reason
    )>?> GetBannedUsersAsync(string channelLogin);

    Task<bool> UnbanByLoginAsync(string channelLogin, string targetLogin);

    /// <summary>Removes every message from the chat room.</summary>
    Task<ModerationDeleteResult> ClearChatAsync(string channelLogin);

    /// <summary>Allows (publishes) or denies a message that AutoMod is holding for review.</summary>
    Task<AutoModDecisionResult> ResolveHeldMessageAsync(string messageId, bool allow);

    Task<HelixClient.ChatSettings?> GetChatSettingsAsync(string channelLogin);

    Task<bool> UpdateChatSettingsAsync(string channelLogin, HelixClient.ChatSettings settings);
}
