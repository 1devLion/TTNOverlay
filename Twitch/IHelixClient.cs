namespace TTNOverlay.Twitch;

/// <summary>
/// Abstraction over the subset of the Twitch Helix API used by the app (viewers, badges, users, chat settings, moderation).
/// </summary>
public interface IHelixClient
{
    bool HasCredentials { get; }

    Task<int?> GetViewerCountAsync(string channelLogin, string userAccessToken);

    Task<Dictionary<string, string>?> GetBadgeMapAsync(string channelLogin, string userAccessToken);

    Task<string?> GetUserIdByLoginAsync(string login, string userAccessToken);

    Task<List<(string Id, string Login)>?> GetChattersAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken
    );

    Task<List<(
        string Id,
        string Login,
        DateTime? ExpiresAt,
        string Reason
    )>?> GetBannedUsersAsync(string broadcasterId, string userAccessToken);

    Task<bool> WarnUserAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken,
        string targetUserId,
        string reason
    );

    Task<bool> BanUserAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken,
        string targetUserId,
        int? durationSeconds = null,
        string? reason = null
    );

    Task<bool> UnbanUserAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken,
        string targetUserId
    );

    Task<HelixClient.ChatSettings?> GetChatSettingsAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken
    );

    Task<bool> UpdateChatSettingsAsync(
        string broadcasterId,
        string moderatorId,
        string userAccessToken,
        HelixClient.ChatSettings settings
    );
}

