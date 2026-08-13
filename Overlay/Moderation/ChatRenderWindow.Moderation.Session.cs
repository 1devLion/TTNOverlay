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

        await RefreshModerationStateAsync();
    }

    private void LogoutFromTwitch()
    {
        _moderation?.Logout();
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

