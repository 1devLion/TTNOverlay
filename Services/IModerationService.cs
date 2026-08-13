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

    Task<HelixClient.ChatSettings?> GetChatSettingsAsync(string channelLogin);

    Task<bool> UpdateChatSettingsAsync(string channelLogin, HelixClient.ChatSettings settings);
}
