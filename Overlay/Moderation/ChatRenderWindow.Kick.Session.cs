using TTNOverlay.Kick;
using TTNOverlay.Models;
using TTNOverlay.Services;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: Kick login session. Keeps this window's settings in sync when the Settings window logs
/// in/out of Kick, and on startup makes sure the stored session carries every scope in
/// KickAuthService.RequiredScopes (same idea as CheckModeratorScopesAsync for Twitch).
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private KickSessionService? _kickSession;

    private void OnKickSessionChanged(KickSessionService source, string refreshToken, string login, string userId)
    {
        if (ReferenceEquals(source, _kickSession))
            return;

        PostToUiThread(() =>
        {
            _settings.KickRefreshToken = refreshToken;
            _settings.KickLogin = login;
            _settings.KickUserId = userId;
            _settings.KickRequestedScopes = source.RequestedScopes;
            _kickSession?.DropCachedToken();

            // Logged out of Kick: its rows can no longer be moderated, so they leave the Messages tab.
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                ClearModerationLog(Platform.Kick);
                _kickSanctions.Clear();
            }
            else
            {
                _ = RefreshKickGrantedScopesAsync();
            }

            EnsureModerationSources();
            RequestRender();
        });
    }

    private async Task CheckKickScopesAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.KickRefreshToken))
                return;

            _kickSession ??= new KickSessionService(_settings);
            var missing = await _kickSession.EnsureRequiredScopesAsync();
            if (missing.Count == 0)
                return;

            PostToUiThread(() =>
            {
                RequestRender();

                ShowConfirmDialog(
                    LocalizationService.T("Kick_ScopesChangedTitle"),
                    LocalizationService.T("Kick_ScopesChangedMessage"),
                    LocalizationService.T("Moderation_ScopesChangedLogin"),
                    confirmed =>
                    {
                        if (confirmed)
                            _ = _kickSession?.LoginAsync();
                    },
                    destructive: false
                );
            });
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("ChatRenderWindow.CheckKickScopesAsync", ex);
        }
    }
}
