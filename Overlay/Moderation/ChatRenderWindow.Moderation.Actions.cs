using TTNOverlay.Services;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: moderation actions (timeout, ban, warn, unban) triggered from the moderation panel.
/// </summary>
internal sealed partial class ChatRenderWindow
{

    private void HandleModerationRowClick(int clientX, int clientY)
    {
        if (_moderationLoginActionRect is { } actionRect && Contains(actionRect, clientX, clientY))
        {
            if (_moderationLoginActionIsLogin)
                _ = LoginWithTwitchAsync();
            else
                LogoutFromTwitch();
            return;
        }

        if (_moderationRefreshActionRect is { } refreshRect && Contains(refreshRect, clientX, clientY))
        {
            _ = RefreshModerationStateAsync();
            return;
        }

        foreach (var (bounds, field) in _moderationChatSettingRowRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                HandleChatSettingCheckboxClick(field);
                return;
            }
        }

        foreach (var (bounds, field) in _moderationChatSettingButtonRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                OpenChatSettingDurationDropdown(bounds, field);
                return;
            }
        }

        foreach (var (bounds, id, login) in _moderationChatterRowRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                OpenChatterActionsDropdown(bounds, id, login);
                return;
            }
        }

        foreach (var (bounds, login, isPermanent) in _moderationBannedRowRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                _ = UnbanRowAsync(login, isPermanent);
                return;
            }
        }
    }

    private void OpenChatterActionsDropdown(Rect anchor, string chatterId, string login)
    {
        var items = new List<ModerationDropdownItem>
    {
        new()
        {
            Label = LocalizationService.T("MainWindow_MuteMenu") + "  \u25b8",
            OnSelect = () => OpenMuteDurationDropdown(anchor, chatterId, login),
        },
        new()
        {
            Label = LocalizationService.T("MainWindow_WarnMenu"),
            OnSelect = () => _ = WarnChatterAsync(chatterId, login),
        },
        new()
        {
            Label = LocalizationService.T("MainWindow_BanMenu"),
            OnSelect = () => _ = BanChatterAsync(chatterId, login),
        },
    };

        OpenModerationDropdown(anchor.Left, anchor.Bottom, items);
    }

    private void OpenMuteDurationDropdown(Rect anchor, string chatterId, string login)
    {
        var items = new List<ModerationDropdownItem>
    {
        new()
        {
            Label = LocalizationService.T("Common_Back"),
            OnSelect = () => OpenChatterActionsDropdown(anchor, chatterId, login),
        },
    };

        foreach (var duration in ModerationMuteDurations)
        {
            int seconds = duration.Seconds;
            items.Add(
                new ModerationDropdownItem
                {
                    Label = LocalizationService.T(duration.LabelKey),
                    OnSelect = () => _ = TimeoutChatterAsync(chatterId, login, seconds),
                }
            );
        }

        OpenModerationDropdown(anchor.Left, anchor.Bottom, items);
    }

    private async Task TimeoutChatterAsync(string chatterId, string login, int seconds)
    {
        if (_moderation is null)
            return;

        _moderationStatusText = string.Format(LocalizationService.T("Moderation_Muting"), login);
        RequestRender();

        var ok = await _moderation.TimeoutAsync(_settings.Channel, chatterId, seconds);

        PostToUiThread(() =>
        {
            _moderationStatusText = string.Format(
                ok ? LocalizationService.T("Moderation_Muted") : LocalizationService.T("Moderation_MuteFailed"),
                login
            );
            RequestRender();
        });
        if (ok)
            _ = LoadBannedUsersAsync();
    }

    private async Task BanChatterAsync(string chatterId, string login)
    {
        if (_moderation is null)
            return;

        ConfirmDialogWindow.Show(
            Hwnd,
            PostToUiThread,
            LocalizationService.T("Moderation_ConfirmBanTitle"),
            string.Format(LocalizationService.T("Moderation_ConfirmBanMessage"), login),
            LocalizationService.T("Moderation_BanButton"),
            confirmed =>
            {
                if (!confirmed)
                    return;

                _moderationStatusText = string.Format(LocalizationService.T("Moderation_Banning"), login);
                RequestRender();
                _ = BanChatterConfirmedAsync(chatterId, login);
            }
        );

        return;

        async Task BanChatterConfirmedAsync(string chatterIdInner, string loginInner)
        {
            var ok = await _moderation.BanAsync(_settings.Channel, chatterIdInner);

            PostToUiThread(() =>
            {
                _moderationStatusText = string.Format(
                    ok ? LocalizationService.T("Moderation_Banned") : LocalizationService.T("Moderation_BanFailed"),
                    loginInner
                );
                RequestRender();
            });
            if (ok)
                _ = LoadBannedUsersAsync();
        }
    }

    private async Task WarnChatterAsync(string chatterId, string login)
    {
        if (_moderation is null)
            return;

        _moderationStatusText = string.Format(LocalizationService.T("Moderation_Warning"), login);
        RequestRender();

        var ok = await _moderation.WarnAsync(
            _settings.Channel,
            chatterId,
            LocalizationService.T("Moderation_ModeratorWarningReason")
        );

        PostToUiThread(() =>
        {
            _moderationStatusText = string.Format(
                ok ? LocalizationService.T("Moderation_Warned") : LocalizationService.T("Moderation_WarnFailed"),
                login
            );
            RequestRender();
        });
    }

    private async Task UnbanRowAsync(string login, bool isPermanent)
    {
        if (_moderation is null)
            return;

        ConfirmDialogWindow.Show(
            Hwnd,
            PostToUiThread,
            LocalizationService.T(isPermanent ? "Moderation_ConfirmUnbanTitle" : "Moderation_ConfirmUnmuteTitle"),
            string.Format(
                LocalizationService.T(isPermanent ? "Moderation_ConfirmUnbanMessage" : "Moderation_ConfirmUnmuteMessage"),
                login
            ),
            LocalizationService.T(isPermanent ? "Moderation_UnbanLabel" : "Moderation_UnmuteLabel"),
            confirmed =>
            {
                if (!confirmed)
                    return;

                _moderationStatusText = string.Format(
                    isPermanent
                        ? LocalizationService.T("Moderation_Unbanning")
                        : LocalizationService.T("Moderation_UnmutingRow"),
                    login
                );
                RequestRender();
                _ = UnbanRowConfirmedAsync(login, isPermanent);
            }
        );

        return;

        async Task UnbanRowConfirmedAsync(string loginInner, bool isPermanentInner)
        {
            var ok = await _moderation.UnbanByLoginAsync(_settings.Channel, loginInner);

            PostToUiThread(() =>
            {
                _moderationStatusText = string.Format(
                    ok
                        ? (
                            isPermanentInner
                                ? LocalizationService.T("Moderation_Unbanned")
                                : LocalizationService.T("Moderation_UnmutedRow")
                        )
                        : (
                            isPermanentInner
                                ? LocalizationService.T("Moderation_UnbanFailed")
                                : LocalizationService.T("Moderation_UnmuteRowFailed")
                        ),
                    loginInner
                );
                RequestRender();
            });
            if (ok)
                await LoadBannedUsersAsync();
        }
    }

}

