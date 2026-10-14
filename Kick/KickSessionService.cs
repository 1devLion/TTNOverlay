using TTNOverlay.Services;

namespace TTNOverlay.Kick;

/// <summary>
/// Holds the Kick login session: login/logout, access-token cache and refresh, and the required-scopes check.
/// Mirrors what ModerationService does for Twitch. Moderation calls (phase 2) will take their token from
/// <see cref="GetAccessTokenAsync"/>.
/// </summary>
public sealed class KickSessionService
{
    private readonly AppSettings _settings;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _accessToken;
    private DateTime _accessTokenExpiresAtUtc = DateTime.MinValue;

    public KickSessionService(AppSettings settings) => _settings = settings;

    /// <summary>
    /// Raised after a login or logout (arguments: the service that did it, then the new refresh token, login and user
    /// id; empty after a logout). Other windows work on their own settings copy, so they use this to sync the session.
    /// </summary>
    public static event Action<KickSessionService, string, string, string>? SessionChanged;

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(_settings.KickRefreshToken);
    public string Login => _settings.KickLogin;
    public string UserId => _settings.KickUserId;

    /// <summary>Scopes requested at login, to be copied to other settings copies when the session changes.</summary>
    public string RequestedScopes => _settings.KickRequestedScopes;

    /// <summary>
    /// The scopes the stored token actually carries (the user can untick some on Kick's consent screen), or null when
    /// not known yet or when Kick couldn't be asked. Callers treat null as "allowed" so a network blip never greys
    /// out the panel.
    /// </summary>
    public IReadOnlyCollection<string>? GrantedScopes { get; private set; }

    public bool? HasScope(string scope) =>
        GrantedScopes is null ? null : GrantedScopes.Contains(scope, StringComparer.OrdinalIgnoreCase);

    /// <summary>Asks Kick which scopes the session carries and caches the answer in <see cref="GrantedScopes"/>.</summary>
    public async Task<IReadOnlyCollection<string>?> RefreshGrantedScopesAsync()
    {
        if (!IsLoggedIn)
        {
            GrantedScopes = null;
            return null;
        }

        var token = await GetAccessTokenAsync();
        if (token is null)
            return GrantedScopes;

        var granted = await KickAuthService.GetGrantedScopesAsync(token);
        if (granted is not null)
            GrantedScopes = granted.ToArray();
        return GrantedScopes;
    }

    public void DropCachedToken()
    {
        _accessToken = null;
        _accessTokenExpiresAtUtc = DateTime.MinValue;
    }

    public async Task<bool> LoginAsync(CancellationToken cancellationToken = default)
    {
        var result = await KickAuthService.LoginAsync(cancellationToken);
        if (result is null)
            return false;

        _accessToken = result.AccessToken;
        _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(result.ExpiresIn - 300, 60));

        _settings.KickRefreshToken = result.RefreshToken;
        _settings.KickLogin = result.Login;
        _settings.KickUserId = result.UserId;
        _settings.KickRequestedScopes = KickAuthService.RequestedScopesText;
        SettingsService.Save(_settings);

        DebugLog.Write($"KickSessionService: login OK as '{result.Login}'");
        _ = RefreshGrantedScopesAsync();
        RaiseSessionChanged();
        return true;
    }

    public void Logout()
    {
        DropCachedToken();
        _settings.KickRefreshToken = "";
        _settings.KickLogin = "";
        _settings.KickUserId = "";
        _settings.KickRequestedScopes = "";
        GrantedScopes = null;
        SettingsService.Save(_settings);
        RaiseSessionChanged();
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (_accessToken != null && DateTime.UtcNow < _accessTokenExpiresAtUtc)
            return _accessToken;

        if (string.IsNullOrWhiteSpace(_settings.KickRefreshToken))
            return null;

        // Kick rotates the refresh token on every refresh, so two concurrent refreshes would invalidate each other.
        await _refreshLock.WaitAsync();
        try
        {
            if (_accessToken != null && DateTime.UtcNow < _accessTokenExpiresAtUtc)
                return _accessToken;

            var refreshed = await KickAuthService.RefreshAsync(_settings.KickRefreshToken);
            if (refreshed is null)
            {
                DebugLog.Write("KickSessionService: could not refresh the Kick token; need to log in again");
                return null;
            }

            _accessToken = refreshed.AccessToken;
            _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(refreshed.ExpiresIn - 300, 60));

            if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                _settings.KickRefreshToken = refreshed.RefreshToken;
            SettingsService.Save(_settings);

            return _accessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Caches the granted scopes and, only when the app itself added a scope since the login (so the user never saw
    /// it on the consent screen), logs out and returns the missing ones to request a new login. A scope the user
    /// deliberately left unticked is not a reason to log out: the panel greys out what it enables instead.
    /// Empty when nothing needs a new login or when that can't be determined (offline, token rejected).
    /// </summary>
    public async Task<IReadOnlyList<string>> EnsureRequiredScopesAsync()
    {
        if (!IsLoggedIn)
            return Array.Empty<string>();

        var token = await GetAccessTokenAsync();
        if (token is null)
            return Array.Empty<string>();

        var granted = await KickAuthService.GetGrantedScopesAsync(token);
        if (granted is null)
            return Array.Empty<string>();

        GrantedScopes = granted.ToArray();

        // Sessions from before this was tracked: assume the current list was requested, and start tracking.
        if (string.IsNullOrWhiteSpace(_settings.KickRequestedScopes))
        {
            _settings.KickRequestedScopes = KickAuthService.RequestedScopesText;
            SettingsService.Save(_settings);
            return Array.Empty<string>();
        }

        var addedSinceLogin = KickAuthService.FindScopesAddedSince(_settings.KickRequestedScopes);
        var missing = KickAuthService.FindMissingScopes(granted).Where(s => addedSinceLogin.Contains(s)).ToList();
        if (missing.Count == 0)
            return missing;

        DebugLog.Write($"KickSessionService: the session lacks scopes [{string.Join(' ', missing)}]; logging out to request a new login");
        Logout();
        return missing;
    }

    private void RaiseSessionChanged()
    {
        try
        {
            SessionChanged?.Invoke(this, _settings.KickRefreshToken, _settings.KickLogin, _settings.KickUserId);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("KickSessionService.RaiseSessionChanged", ex);
        }
    }
}
