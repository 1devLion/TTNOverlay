using TTNOverlay.Twitch;

namespace TTNOverlay.Services;

/// <summary>
/// Abstraction over Twitch chat moderation actions (login state, chatters, timeouts/bans) used by the moderation panel.
/// </summary>
public interface IModerationService
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

    Task<bool> WarnAsync(string channelLogin, string targetUserId, string reason);

    Task<bool> TimeoutAsync(
        string channelLogin,
        string targetUserId,
        int durationSeconds,
        string? reason = null
    );

    Task<bool> BanAsync(string channelLogin, string targetUserId, string? reason = null);

    Task<bool> UnbanByLoginAsync(string channelLogin, string targetLogin);

    /// <summary>Deletes a single chat message (Twitch only allows messages from the last 6 hours, not the broadcaster's or other moderators').</summary>
    Task<ModerationDeleteResult> DeleteMessageAsync(string channelLogin, string messageId);

    /// <summary>Removes every message from the chat room.</summary>
    Task<ModerationDeleteResult> ClearChatAsync(string channelLogin);

    /// <summary>Allows (publishes) or denies a message that AutoMod is holding for review.</summary>
    Task<AutoModDecisionResult> ResolveHeldMessageAsync(string messageId, bool allow);

    Task<HelixClient.ChatSettings?> GetChatSettingsAsync(string channelLogin);

    Task<bool> UpdateChatSettingsAsync(string channelLogin, HelixClient.ChatSettings settings);
}
