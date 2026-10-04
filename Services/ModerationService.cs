using TTNOverlay.Twitch;

namespace TTNOverlay.Services;

/// <summary>
/// Default IModerationService implementation backed by the Twitch Helix API.
/// </summary>
public class ModerationService : IModerationService
{
    private readonly IHelixClient _helix;
    private readonly AppSettings _settings;

    private string? _accessToken;
    private DateTime _accessTokenExpiresAtUtc = DateTime.MinValue;

    // login -> user id. A channel's id never changes, so resolve it once instead of paying an extra GET /users
    // round trip in front of every moderation action.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _broadcasterIds =
        new(StringComparer.OrdinalIgnoreCase);

    public ModerationService(AppSettings settings)
        : this(settings, new HelixClient(TwitchAuthService.ClientId)) { }

    public ModerationService(AppSettings settings, IHelixClient helix)
    {
        _settings = settings;
        _helix = helix;
    }

    /// <summary>
    /// Raised after a login or logout (arguments: the service that did it, then the new refresh token, login and user
    /// id; empty after a logout). Each window works on its own copy of the settings and keeps its own cached access
    /// token, so the others use this to copy the session and drop the token they had cached.
    /// </summary>
    public static event Action<ModerationService, string, string, string>? SessionChanged;

    /// <summary>Forgets the cached access token so the next call refreshes it from the stored refresh token.</summary>
    public void DropCachedToken()
    {
        _accessToken = null;
        _accessTokenExpiresAtUtc = DateTime.MinValue;
        _broadcasterIds.Clear();
    }

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(_settings.ModeratorRefreshToken);

    public string ModeratorLogin => _settings.ModeratorLogin;

    public bool HasCredentials => _settings.EnableTwitchApi;

    public async Task<bool> LoginAsync(CancellationToken cancellationToken = default)
    {
        var result = await TwitchAuthService.LoginAsync(cancellationToken);
        if (result is null)
            return false;

        _accessToken = result.AccessToken;
        _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(result.ExpiresIn - 300, 60));

        _settings.ModeratorRefreshToken = result.RefreshToken;
        _settings.ModeratorLogin = result.Login;
        _settings.ModeratorUserId = result.UserId;
        SettingsService.Save(_settings);

        DebugLog.Write($"ModerationService: login OK as '{result.Login}'");
        RaiseSessionChanged();
        return true;
    }

    public void Logout()
    {
        _accessToken = null;
        _accessTokenExpiresAtUtc = DateTime.MinValue;
        _broadcasterIds.Clear();
        _settings.ModeratorRefreshToken = "";
        _settings.ModeratorLogin = "";
        _settings.ModeratorUserId = "";
        SettingsService.Save(_settings);
        RaiseSessionChanged();
    }

    private void RaiseSessionChanged()
    {
        try
        {
            SessionChanged?.Invoke(
                this,
                _settings.ModeratorRefreshToken,
                _settings.ModeratorLogin,
                _settings.ModeratorUserId
            );
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("ModerationService.RaiseSessionChanged", ex);
        }
    }

    internal void SeedAccessTokenForTests(string accessToken, DateTime? expiresAtUtc = null)
    {
        _accessToken = accessToken;
        _accessTokenExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddHours(1);
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (_accessToken != null && DateTime.UtcNow < _accessTokenExpiresAtUtc)
            return _accessToken;

        if (string.IsNullOrWhiteSpace(_settings.ModeratorRefreshToken))
            return null;

        var refreshed = await TwitchAuthService.RefreshAsync(_settings.ModeratorRefreshToken);
        if (refreshed is null)
        {
            DebugLog.Write(
                "ModerationService: could not refresh the moderator token; need to log in again"
            );
            return null;
        }

        _accessToken = refreshed.AccessToken;
        _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(
            Math.Max(refreshed.ExpiresIn - 300, 60)
        );

        if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
            _settings.ModeratorRefreshToken = refreshed.RefreshToken;
        SettingsService.Save(_settings);

        return _accessToken;
    }

    public async Task<List<(string Id, string Login)>?> GetChattersAsync(string channelLogin)
    {
        var token = await GetAccessTokenAsync();
        if (token is null)
            return null;

        var broadcasterId = await ResolveBroadcasterIdAsync(channelLogin, token);
        if (broadcasterId is null)
            return null;

        return await _helix.GetChattersAsync(broadcasterId, _settings.ModeratorUserId, token);
    }

    public async Task<List<(
        string Id,
        string Login,
        DateTime? ExpiresAt,
        string Reason
    )>?> GetBannedUsersAsync(string channelLogin)
    {
        var token = await GetAccessTokenAsync();
        if (token is null)
            return null;

        var broadcasterId = await ResolveBroadcasterIdAsync(channelLogin, token);
        if (broadcasterId is null)
            return null;

        return await _helix.GetBannedUsersAsync(broadcasterId, token);
    }

    public async Task<bool> WarnAsync(string channelLogin, string targetUserId, string reason)
    {
        var token = await GetAccessTokenAsync();
        if (token is null)
            return false;

        var broadcasterId = await ResolveBroadcasterIdAsync(channelLogin, token);
        if (broadcasterId is null)
            return false;

        return await _helix.WarnUserAsync(
            broadcasterId,
            _settings.ModeratorUserId,
            token,
            targetUserId,
            reason
        );
    }

    public Task<bool> TimeoutAsync(
        string channelLogin,
        string targetUserId,
        int durationSeconds,
        string? reason = null
    ) => BanOrTimeoutAsync(channelLogin, targetUserId, durationSeconds, reason);

    public Task<bool> BanAsync(string channelLogin, string targetUserId, string? reason = null) =>
        BanOrTimeoutAsync(channelLogin, targetUserId, null, reason);

    private async Task<bool> BanOrTimeoutAsync(
        string channelLogin,
        string targetUserId,
        int? durationSeconds,
        string? reason
    )
    {
        var token = await GetAccessTokenAsync();
        if (token is null)
            return false;

        var broadcasterId = await ResolveBroadcasterIdAsync(channelLogin, token);
        if (broadcasterId is null)
            return false;

        return await _helix.BanUserAsync(
            broadcasterId,
            _settings.ModeratorUserId,
            token,
            targetUserId,
            durationSeconds,
            reason
        );
    }

    public async Task<bool> UnbanByLoginAsync(string channelLogin, string targetLogin)
    {
        var token = await GetAccessTokenAsync();
        if (token is null)
            return false;

        var broadcasterId = await ResolveBroadcasterIdAsync(channelLogin, token);
        if (broadcasterId is null)
            return false;

        var targetId = await _helix.GetUserIdByLoginAsync(targetLogin, token);
        if (targetId is null)
            return false;

        return await _helix.UnbanUserAsync(
            broadcasterId,
            _settings.ModeratorUserId,
            token,
            targetId
        );
    }

    public async Task<ModerationDeleteResult> DeleteMessageAsync(string channelLogin, string messageId) =>
        await DeleteChatMessagesCoreAsync(channelLogin, messageId);

    public async Task<ModerationDeleteResult> ClearChatAsync(string channelLogin) =>
        await DeleteChatMessagesCoreAsync(channelLogin, null);

    private async Task<ModerationDeleteResult> DeleteChatMessagesCoreAsync(string channelLogin, string? messageId)
    {
        var token = await GetAccessTokenAsync();
        if (token is null)
            return ModerationDeleteResult.MissingPermission;

        var broadcasterId = await ResolveBroadcasterIdAsync(channelLogin, token);
        if (broadcasterId is null)
            return ModerationDeleteResult.Failed;

        return await _helix.DeleteChatMessagesAsync(broadcasterId, _settings.ModeratorUserId, token, messageId);
    }

    private async Task<string?> ResolveBroadcasterIdAsync(string channelLogin, string token)
    {
        if (_broadcasterIds.TryGetValue(channelLogin, out var cached))
            return cached;

        var resolved = await _helix.GetUserIdByLoginAsync(channelLogin, token);
        if (resolved is not null)
            _broadcasterIds[channelLogin] = resolved;
        return resolved;
    }

    public async Task<HelixClient.ChatSettings?> GetChatSettingsAsync(string channelLogin)
    {
        var token = await GetAccessTokenAsync();
        if (token is null)
            return null;

        var broadcasterId = await ResolveBroadcasterIdAsync(channelLogin, token);
        if (broadcasterId is null)
            return null;

        return await _helix.GetChatSettingsAsync(broadcasterId, _settings.ModeratorUserId, token);
    }

    public async Task<bool> UpdateChatSettingsAsync(
        string channelLogin,
        HelixClient.ChatSettings settings
    )
    {
        var token = await GetAccessTokenAsync();
        if (token is null)
            return false;

        var broadcasterId = await ResolveBroadcasterIdAsync(channelLogin, token);
        if (broadcasterId is null)
            return false;

        return await _helix.UpdateChatSettingsAsync(
            broadcasterId,
            _settings.ModeratorUserId,
            token,
            settings
        );
    }
}