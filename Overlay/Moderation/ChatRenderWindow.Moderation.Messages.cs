using TTNOverlay.Models;
using TTNOverlay.Services;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the moderation panel's "Messages" tab. Recent Twitch chat is kept in a bounded
/// <see cref="ModerationMessageLog"/> (strings only, a few hundred KB at its 2000-entry cap) and shown for a
/// selectable time window, with per-message actions: delete, warn, mute, ban, combinations of those, delete all of a
/// user's messages, and clear the whole chat. Nothing here runs unless the user is logged in as a moderator.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private enum ModerationTab
    {
        Chatters,
        Messages,
        AutoMod,
    }

    private static readonly (ModerationTab Tab, string LabelKey)[] ModerationTabs =
    {
        (ModerationTab.Chatters, "Moderation_TabChatters"),
        (ModerationTab.Messages, "Moderation_TabMessages"),
        (ModerationTab.AutoMod, "Moderation_TabAutoMod"),
    };

    private static readonly int[] ModerationMessageWindowChoices = { 1, 5, 10, 30, 60 };

    private const float MessageActionsDropdownWidth = 270f;

    private ModerationTab _moderationTab = ModerationTab.Chatters;

    private readonly ModerationMessageLog _moderationLog = new();

    private readonly List<(Rect Bounds, ModerationTab Tab)> _moderationTabRects = new();
    private readonly List<(Rect Bounds, ModerationLogEntry Entry)> _moderationMessageActionRects = new();
    private Rect? _moderationWindowPillRect;
    private Rect? _moderationClearChatPillRect;

    /// <summary>
    /// Bumped whenever something that changes a row's measured height changes (language, fonts), so cached
    /// per-row heights are measured again.
    /// </summary>
    private int _moderationLayoutVersion = 1;

    private int ModerationWindowMinutes =>
        Array.IndexOf(ModerationMessageWindowChoices, _settings.ModerationMessagesWindowMinutes) >= 0
            ? _settings.ModerationMessagesWindowMinutes
            : 10;

    private bool ModerationMessagesVisible => _showingModeration && _moderationTab == ModerationTab.Messages;

    // ------------------------------------------------------------------ feeding the log

    /// <summary>
    /// Records a chat message for the Messages tab. Called on the UI thread from the shared incoming-message path.
    /// Skips everything unless a moderator is logged in, and only keeps Twitch messages (the only ones that carry the
    /// ids moderation needs). The text/name strings are the same instances the chat list holds, not copies.
    /// </summary>
    private void LogMessageForModeration(ChatMessage msg)
    {
        if (msg.IsSystem || msg.MessageId is null || msg.UserId is null)
            return;
        if (!_settings.EnableModerationPanel || string.IsNullOrWhiteSpace(_settings.ModeratorRefreshToken))
            return;

        bool isProtected =
            string.Equals(msg.UserId, _settings.ModeratorUserId, StringComparison.Ordinal) || HasProtectedBadge(msg);

        // Twitch sends no event for unbans, but a user who can write again is not banned/timed out anymore.
        _moderationLog.ClearUserState(msg.UserId);

        _moderationLog.Add(
            new ModerationLogEntry(
                msg.MessageId,
                msg.UserId,
                msg.Username,
                msg.DisplayName,
                msg.Text,
                msg.ReceivedAt,
                isProtected
            )
        );
    }

    /// <summary>Broadcaster, moderators and staff can't be deleted/muted/banned through the API, so no actions are offered on them.</summary>
    private static bool HasProtectedBadge(ChatMessage msg)
    {
        foreach (var badge in msg.Badges)
        {
            switch (badge.Name)
            {
                case "broadcaster":
                case "moderator":
                case "lead_moderator":
                case "staff":
                case "admin":
                case "global_mod":
                    return true;
            }
        }
        return false;
    }

    private void OnIrcMessageDeleted(string messageId) =>
        PostToUiThread(() =>
        {
            if (_moderationLog.MarkMessageDeleted(messageId) && ModerationMessagesVisible)
                RequestRender();
        });

    private void OnIrcUserPurged(string userId, int? timeoutSeconds) =>
        PostToUiThread(() =>
        {
            var state = timeoutSeconds is null ? ModerationUserState.Banned : ModerationUserState.TimedOut;
            if (_moderationLog.MarkUserState(userId, state) > 0 && ModerationMessagesVisible)
                RequestRender();
        });

    private void OnIrcChatCleared() =>
        PostToUiThread(() =>
        {
            _moderationLog.MarkAllDeleted();
            if (ModerationMessagesVisible)
                RequestRender();
        });

    private void ClearModerationLog()
    {
        _moderationLog.Clear();
        _moderationMessagesLastNewest = null;
        _moderationMessagesScroll = default;
        _autoModQueue.Clear();
    }

    // ------------------------------------------------------------------ clicks

    private void SwitchModerationTab(ModerationTab tab)
    {
        if (tab == _moderationTab)
            return;

        CloseModerationDropdown();

        // The Chatters and AutoMod tabs share one top-anchored scroll state, so a stale offset must not carry over.
        if (tab == ModerationTab.AutoMod || _moderationTab == ModerationTab.AutoMod)
            _moderationScroll = default;

        _moderationTab = tab;
        _hoveredChatSettingButton = null;
        _moderationLoginButtonHovered = false;
        RequestRender();
    }

    private void HandleMessagesTabClick(int clientX, int clientY)
    {
        if (_moderationWindowPillRect is { } windowRect && Contains(windowRect, clientX, clientY))
        {
            OpenMessageWindowDropdown(windowRect);
            return;
        }

        if (_moderationClearChatPillRect is { } clearRect && Contains(clearRect, clientX, clientY))
        {
            ConfirmClearChat();
            return;
        }

        foreach (var (bounds, entry) in _moderationMessageActionRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                OpenMessageActionsDropdown(bounds, entry);
                return;
            }
        }
    }

    private void OpenMessageWindowDropdown(Rect anchor)
    {
        int current = ModerationWindowMinutes;
        var items = new List<ModerationDropdownItem>();
        foreach (int minutes in ModerationMessageWindowChoices)
        {
            int selected = minutes;
            items.Add(
                new ModerationDropdownItem
                {
                    Label =
                        (selected == current ? "\u2713  " : "     ")
                        + string.Format(LocalizationService.T("Moderation_MsgWindowLabel"), selected),
                    OnSelect = () => SetModerationWindow(selected),
                }
            );
        }
        OpenModerationDropdown(anchor.Left, anchor.Bottom, items);
    }

    private void SetModerationWindow(int minutes)
    {
        _settings.ModerationMessagesWindowMinutes = minutes;
        SettingsService.Save(_settings);
        _moderationMessagesScroll = default;
        _moderationMessagesLastNewest = null;
        RequestRender();
    }

    private void OpenMessageActionsDropdown(Rect anchor, ModerationLogEntry entry)
    {
        var items = new List<ModerationDropdownItem>();

        if (!entry.IsDeleted)
        {
            items.Add(
                new ModerationDropdownItem
                {
                    Label = LocalizationService.T("Moderation_ActDelete"),
                    OnSelect = () => _ = RunMessageActionAsync(entry, true, ModerationSanction.None),
                }
            );
            items.Add(
                new ModerationDropdownItem
                {
                    Label = LocalizationService.T("Moderation_ActDeleteWarn"),
                    OnSelect = () => _ = RunMessageActionAsync(entry, true, ModerationSanction.Warn),
                }
            );
            items.Add(
                new ModerationDropdownItem
                {
                    Label = LocalizationService.T("Moderation_ActDeleteMute") + "  \u25b8",
                    OnSelect = () => OpenMessageMuteDurationDropdown(anchor, entry, deleteToo: true),
                }
            );
            items.Add(
                new ModerationDropdownItem
                {
                    Label = LocalizationService.T("Moderation_ActDeleteBan"),
                    OnSelect = () => ConfirmBanFromMessage(entry, deleteToo: true),
                }
            );
        }

        items.Add(
            new ModerationDropdownItem
            {
                Label = LocalizationService.T("MainWindow_MuteMenu") + "  \u25b8",
                OnSelect = () => OpenMessageMuteDurationDropdown(anchor, entry, deleteToo: false),
            }
        );
        items.Add(
            new ModerationDropdownItem
            {
                Label = LocalizationService.T("MainWindow_WarnMenu"),
                OnSelect = () => _ = RunMessageActionAsync(entry, false, ModerationSanction.Warn),
            }
        );
        items.Add(
            new ModerationDropdownItem
            {
                Label = LocalizationService.T("MainWindow_BanMenu"),
                OnSelect = () => ConfirmBanFromMessage(entry, deleteToo: false),
            }
        );
        items.Add(
            new ModerationDropdownItem
            {
                Label = LocalizationService.T("Moderation_ActDeleteAllFromUser"),
                OnSelect = () => ConfirmDeleteAllFromUser(entry),
            }
        );

        OpenModerationDropdown(anchor.Left, anchor.Bottom, items, MessageActionsDropdownWidth);
    }

    private void OpenMessageMuteDurationDropdown(Rect anchor, ModerationLogEntry entry, bool deleteToo)
    {
        var items = new List<ModerationDropdownItem>
        {
            new()
            {
                Label = LocalizationService.T("Common_Back"),
                OnSelect = () => OpenMessageActionsDropdown(anchor, entry),
            },
        };

        foreach (var duration in ModerationMuteDurations)
        {
            int seconds = duration.Seconds;
            items.Add(
                new ModerationDropdownItem
                {
                    Label = LocalizationService.T(duration.LabelKey),
                    OnSelect = () => _ = RunMessageActionAsync(entry, deleteToo, ModerationSanction.Timeout, seconds),
                }
            );
        }

        OpenModerationDropdown(anchor.Left, anchor.Bottom, items, MessageActionsDropdownWidth);
    }

    // ------------------------------------------------------------------ actions

    private void ConfirmBanFromMessage(ModerationLogEntry entry, bool deleteToo)
    {
        ConfirmDialogWindow.Show(
            Hwnd,
            PostToUiThread,
            LocalizationService.T("Moderation_ConfirmBanTitle"),
            string.Format(LocalizationService.T("Moderation_ConfirmBanMessage"), entry.Login),
            LocalizationService.T("Moderation_BanButton"),
            confirmed =>
            {
                if (confirmed)
                    _ = RunMessageActionAsync(entry, deleteToo, ModerationSanction.Ban);
            }
        );
    }

    /// <summary>Deletes the message and/or applies a sanction to its author, then reports the outcome in the status line.</summary>
    private async Task RunMessageActionAsync(
        ModerationLogEntry entry,
        bool deleteMessage,
        ModerationSanction sanction,
        int timeoutSeconds = 0
    )
    {
        var moderation = _moderation;
        if (moderation is null)
            return;

        var channel = _settings.Channel;
        var name = DisplayNameOf(entry);

        _moderationStatusText = string.Format(LocalizationService.T("Moderation_Working"), name);
        RequestRender();

        var outcome = await ModerationActionRunner.RunAsync(
            moderation,
            new ModerationActionRequest(
                channel,
                entry.UserId,
                deleteMessage ? entry.MessageId : null,
                sanction,
                timeoutSeconds,
                LocalizationService.T("Moderation_ModeratorWarningReason")
            )
        );

        PostToUiThread(() =>
        {
            if (outcome.Delete == ModerationDeleteResult.Ok)
            {
                entry.IsDeleted = true;
                entry.InvalidateLayout();
            }

            _moderationStatusText = BuildActionStatus(entry, name, sanction, outcome);
            RequestRender();
        });

        if (outcome.Sanction == true && sanction is ModerationSanction.Timeout or ModerationSanction.Ban)
            _ = LoadBannedUsersAsync();
    }

    private static string BuildActionStatus(
        ModerationLogEntry entry,
        string name,
        ModerationSanction sanction,
        ModerationActionOutcome outcome
    )
    {
        var parts = new List<string>(2);

        if (outcome.Delete is { } delete)
            parts.Add(DeleteStatus(delete, name));

        if (outcome.Sanction is { } sanctionOk)
        {
            string key = (sanction, sanctionOk) switch
            {
                (ModerationSanction.Warn, true) => "Moderation_Warned",
                (ModerationSanction.Warn, false) => "Moderation_WarnFailed",
                (ModerationSanction.Timeout, true) => "Moderation_Muted",
                (ModerationSanction.Timeout, false) => "Moderation_MuteFailed",
                (_, true) => "Moderation_Banned",
                (_, false) => "Moderation_BanFailed",
            };
            parts.Add(string.Format(LocalizationService.T(key), entry.Login));
        }

        return string.Join(" ", parts);
    }

    private static string DeleteStatus(ModerationDeleteResult result, string name) =>
        result switch
        {
            ModerationDeleteResult.Ok => string.Format(LocalizationService.T("Moderation_MsgDeleted"), name),
            ModerationDeleteResult.MissingPermission => LocalizationService.T("Moderation_DeleteNeedsRelogin"),
            ModerationDeleteResult.Rejected => LocalizationService.T("Moderation_MsgDeleteRejected"),
            _ => string.Format(LocalizationService.T("Moderation_MsgDeleteFailed"), name),
        };

    private void ConfirmDeleteAllFromUser(ModerationLogEntry entry)
    {
        var ids = new List<string>();
        _moderationLog.CollectDeletableMessageIds(entry.UserId, DateTime.UtcNow.AddMinutes(-ModerationWindowMinutes), ids);
        if (ids.Count == 0)
            return;

        var name = DisplayNameOf(entry);
        ConfirmDialogWindow.Show(
            Hwnd,
            PostToUiThread,
            LocalizationService.T("Moderation_ConfirmDeleteAllTitle"),
            string.Format(LocalizationService.T("Moderation_ConfirmDeleteAllMessage"), ids.Count, name),
            LocalizationService.T("Moderation_DeleteButton"),
            confirmed =>
            {
                if (confirmed)
                    _ = DeleteAllFromUserAsync(ids, name);
            }
        );
    }

    private async Task DeleteAllFromUserAsync(List<string> ids, string name)
    {
        var moderation = _moderation;
        if (moderation is null)
            return;

        _moderationStatusText = string.Format(LocalizationService.T("Moderation_Working"), name);
        RequestRender();

        var outcome = await ModerationActionRunner.DeleteManyAsync(
            moderation,
            _settings.Channel,
            ids,
            onMessageDeleted: id => PostToUiThread(() => _moderationLog.MarkMessageDeleted(id))
        );

        // Queued after every per-message mark above (the UI queue is FIFO), so this single render shows them all.
        PostToUiThread(() =>
        {
            _moderationStatusText = outcome.MissingPermission
                ? LocalizationService.T("Moderation_DeleteNeedsRelogin")
                : string.Format(LocalizationService.T("Moderation_MsgBulkResult"), outcome.Deleted, ids.Count, name);
            RequestRender();
        });
    }

    private void ConfirmClearChat()
    {
        ConfirmDialogWindow.Show(
            Hwnd,
            PostToUiThread,
            LocalizationService.T("Moderation_ConfirmClearChatTitle"),
            LocalizationService.T("Moderation_ConfirmClearChatMessage"),
            LocalizationService.T("Moderation_ClearButton"),
            confirmed =>
            {
                if (confirmed)
                    _ = ClearChatAsync();
            }
        );
    }

    private async Task ClearChatAsync()
    {
        var moderation = _moderation;
        if (moderation is null)
            return;

        _moderationStatusText = LocalizationService.T("Moderation_ClearingChat");
        RequestRender();

        var result = await moderation.ClearChatAsync(_settings.Channel);

        PostToUiThread(() =>
        {
            if (result == ModerationDeleteResult.Ok)
            {
                _moderationLog.MarkAllDeleted();
                _moderationStatusText = LocalizationService.T("Moderation_ChatCleared");
            }
            else
            {
                _moderationStatusText =
                    result == ModerationDeleteResult.MissingPermission
                        ? LocalizationService.T("Moderation_DeleteNeedsRelogin")
                        : LocalizationService.T("Moderation_ClearChatFailed");
            }
            RequestRender();
        });
    }

    private static string DisplayNameOf(ModerationLogEntry entry) =>
        string.IsNullOrEmpty(entry.DisplayName) ? entry.Login : entry.DisplayName;
}
