using TTNOverlay.Services;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: on startup, makes sure the stored Twitch session carries every scope the app requires
/// (TwitchAuthService.RequiredScopes). When a release adds a feature that needs a new permission, the old session
/// can't have it, so we log out and ask the user to log in again.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private async Task CheckModeratorScopesAsync()
    {
        try
        {
            if (!_settings.EnableTwitchApi || string.IsNullOrWhiteSpace(_settings.ModeratorRefreshToken))
                return;

            _moderation ??= new ModerationService(_settings);
            var missing = await _moderation.EnsureRequiredScopesAsync();
            if (missing.Count == 0)
                return;

            PostToUiThread(() =>
            {
                // Same cleanup as a manual logout (the service already cleared the stored session).
                DisconnectEventSub();
                ClearModerationLog();
                _moderationChatters = new();
                _moderationBanned = null;
                _moderationChatSettings = null;
                if (_showingModeration)
                    _ = RefreshModerationStateAsync();
                RequestRender();

                ShowConfirmDialog(
                    LocalizationService.T("Moderation_ScopesChangedTitle"),
                    LocalizationService.T("Moderation_ScopesChangedMessage"),
                    LocalizationService.T("Moderation_ScopesChangedLogin"),
                    confirmed =>
                    {
                        if (confirmed)
                            _ = LoginWithTwitchAsync();
                    },
                    destructive: false
                );
            });
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("ChatRenderWindow.CheckModeratorScopesAsync", ex);
        }
    }
}
