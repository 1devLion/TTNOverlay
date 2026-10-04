using TTNOverlay.Services;
using TTNOverlay.Twitch;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: moderator login/logout and loading chatters, banned users, and chat settings for the panel.
/// </summary>
internal sealed partial class ChatRenderWindow
{

    private async Task RefreshModerationStateAsync()
    {
        if (_moderation is null)
            return;

        if (!_moderation.HasCredentials)
        {
            _moderationStatusText = LocalizationService.T("Moderation_TwitchDisabled");
            _moderationCountText = "";
            _moderationChatters = new();
            _moderationBanned = null;
            _moderationChatSettings = null;
            RequestRender();
            return;
        }

        if (!_moderation.IsLoggedIn)
        {
            _moderationStatusText = LocalizationService.T("Moderation_LoginPrompt");
            _moderationCountText = "";
            _moderationChatters = new();
            _moderationBanned = null;
            _moderationChatSettings = null;
            RequestRender();
            return;
        }

        await LoadModerationChattersAsync();
        _ = LoadBannedUsersAsync();
        _ = LoadChatSettingsAsync();
    }

    private async Task LoginWithTwitchAsync()
    {
        if (_moderation is null)
            return;

        _moderationStatusText = LocalizationService.T("Moderation_OpeningBrowser");
        RequestRender();

        var ok = await _moderation.LoginAsync();
        if (!ok)
        {

            PostToUiThread(() =>
            {
                _moderationStatusText = LocalizationService.T("Moderation_LoginFailed");
                RequestRender();
            });
            return;
        }

        PostToUiThread(ConnectEventSubIfEligible);
        await RefreshModerationStateAsync();
    }

    /// <summary>
    /// The Twitch API tab of the settings window logged in or out. That window edits a copy of the settings, so copy
    /// the session over here (the copy replaces <c>_settings</c> when the window closes) and refresh the panel.
    /// </summary>
    private void OnModeratorSessionChanged(ModerationService source, string refreshToken, string login, string userId)
    {
        if (ReferenceEquals(source, _moderation))
            return;

        PostToUiThread(() =>
        {
            _settings.ModeratorRefreshToken = refreshToken;
            _settings.ModeratorLogin = login;
            _settings.ModeratorUserId = userId;
            _moderation?.DropCachedToken();

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                DisconnectEventSub();
                ClearModerationLog();
                _moderationChatters = new();
                _moderationBanned = null;
            }
            else
            {
                ConnectEventSubIfEligible();
            }

            if (_showingModeration)
                _ = RefreshModerationStateAsync();
            RequestRender();
        });
    }

    private void LogoutFromTwitch()
    {
        _moderation?.Logout();
        DisconnectEventSub();
        ClearModerationLog();
        _moderationChatters = new();
        _moderationBanned = null;

        _ = RefreshModerationStateAsync();
    }

    private async Task LoadModerationChattersAsync()
    {
        if (_moderation is null || string.IsNullOrWhiteSpace(_settings.Channel))
            return;

        _moderationStatusText = LocalizationService.T("Moderation_LoadingChatters");
        RequestRender();

        var chatters = await _moderation.GetChattersAsync(_settings.Channel);

        PostToUiThread(() =>
        {
            if (chatters is null)
            {
                _moderationChatters = new();
                _moderationStatusText = LocalizationService.T("Moderation_ChattersLoadFailed");
                _moderationCountText = "";
            }
            else
            {
                _moderationChatters = chatters;
                _moderationStatusText = string.Format(
                    LocalizationService.T("Moderation_SessionLabel"),
                    _moderation.ModeratorLogin
                );
                _moderationCountText = string.Format(
                    LocalizationService.T("Moderation_ConnectedCount"),
                    chatters.Count
                );
            }
            RequestRender();
        });
    }

    private async Task LoadBannedUsersAsync()
    {
        if (_moderation is null || string.IsNullOrWhiteSpace(_settings.Channel))
            return;

        var banned = await _moderation.GetBannedUsersAsync(_settings.Channel);

        PostToUiThread(() =>
        {
            _moderationBanned = banned;

            // Rows tagged [muted]/[banned] for someone who is no longer on Twitch's list (unbanned from Twitch itself,
            // or a timeout that ran out) lose their tag.
            if (banned is not null)
            {
                var restricted = new HashSet<string>(banned.Count, StringComparer.Ordinal);
                foreach (var b in banned)
                    restricted.Add(b.Id);
                if (_moderationLog.ReconcileUserStates(restricted) > 0 && ModerationMessagesVisible)
                    RequestRender();
            }

            RequestRender();
        });
    }

    private async Task LoadChatSettingsAsync()
    {
        if (_moderation is null || string.IsNullOrWhiteSpace(_settings.Channel))
            return;

        var settings = await _moderation.GetChatSettingsAsync(_settings.Channel);
        if (settings is null)
            return;

        PostToUiThread(() =>
        {
            _moderationChatSettings = settings;
            RequestRender();
        });
    }
}

