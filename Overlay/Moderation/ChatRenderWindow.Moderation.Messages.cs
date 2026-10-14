using TTNOverlay.Kick;
using TTNOverlay.Models;
using TTNOverlay.Services;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the moderation panel's "Messages" tab. Recent Twitch chat is kept in a bounded
/// <see cref="ModerationMessageLog"/> (strings only, a few hundred KB at its 2000-entry cap) and shown for a
/// selectable time window, with per-message actions: delete, warn, mute, ban, combinations of those, delete all of a
/// user's messages, and clear the whole chat. Twitch and Kick messages share the list; each row's actions go to its own
/// platform (Kick has no warn and no clear-chat in its public API, so those are only offered on Twitch). Nothing here
/// runs for a platform unless the user is logged in to it as a moderator.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private enum ModerationTab
    {
        Chatters,
        Messages,
        AutoMod,
        Sanctions,
    }

    private static readonly (ModerationTab Tab, string LabelKey)[] ModerationTabs =
    {
        (ModerationTab.Chatters, "Moderation_TabChatters"),
        (ModerationTab.Messages, "Moderation_TabMessages"),
        (ModerationTab.AutoMod, "Moderation_TabAutoMod"),
        (ModerationTab.Sanctions, "Moderation_TabSanctions"),
    };

    private static readonly int[] ModerationMessageWindowChoices = { 1, 5, 10, 30, 60 };

    private const float MessageActionsDropdownWidth = 270f;

    private const int KickMaxTimeoutSeconds = 7 * 24 * 60 * 60;

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

    private KickModerationService? _kickModeration;

    private bool TwitchModerationReady => _moderation is { HasCredentials: true, IsLoggedIn: true };

    /// <summary>Whether the panel, in its current mode, has a logged-in platform to work with.</summary>
    private bool ModerationHasUsablePlatform =>
        (ModerationShowsTwitch && TwitchModerationReady) || (ModerationShowsKick && KickModerationReady);

    private bool KickModerationReady => !string.IsNullOrWhiteSpace(_settings.KickRefreshToken);

    /// <summary>
    /// The moderation actions and channel for one platform, or false when that platform isn't usable right now (not
    /// logged in, or its chat isn't connected so there is no channel to act on).
    /// </summary>
    private bool TryGetModerationActions(Platform platform, out IChatModerationActions actions, out string channel)
    {
        if (platform == Platform.Kick)
        {
            actions = null!;
            channel = KickModerationChannel;
            if (!KickModerationReady || string.IsNullOrWhiteSpace(channel))
                return false;

            _kickSession ??= new KickSessionService(_settings);
            _kickModeration ??= new KickModerationService(_kickSession);
            actions = _kickModeration;
            return true;
        }

        actions = null!;
        channel = _settings.Channel;
        if (_moderation is null)
            return false;

        actions = _moderation;
        return true;
    }

    /// <summary>Which message-delete-needs-relogin text applies to a platform.</summary>
    private static string DeleteNeedsReloginText(Platform platform) =>
        LocalizationService.T(platform == Platform.Kick ? "Kick_DeleteNeedsRelogin" : "Moderation_DeleteNeedsRelogin");

    // ------------------------------------------------------------------ feeding the log

    /// <summary>
    /// Records a chat message for the Messages tab. Called on the UI thread from the shared incoming-message path.
    /// Skips everything unless a moderator is logged in to that message's platform, and only keeps messages that carry
    /// the ids moderation needs. The text/name strings are the same instances the chat list holds, not copies.
    /// </summary>
    private void LogMessageForModeration(ChatMessage msg)
    {
        if (msg.IsSystem || msg.MessageId is null || msg.UserId is null)
            return;
        if (!_settings.EnableModerationPanel)
            return;

        var platform = msg.SourcePlatform ?? Platform.Twitch;
        bool isKick = platform == Platform.Kick;
        if (string.IsNullOrWhiteSpace(isKick ? _settings.KickRefreshToken : _settings.ModeratorRefreshToken))
            return;

        string moderatorUserId = isKick ? _settings.KickUserId : _settings.ModeratorUserId;
        bool isProtected =
            string.Equals(msg.UserId, moderatorUserId, StringComparison.Ordinal) || HasProtectedBadge(msg);

        // Neither platform tells us about unbans through chat, but a user who can write again is not banned/timed out anymore.
        _moderationLog.ClearUserState(msg.UserId, platform);

        _moderationLog.Add(
            new ModerationLogEntry(
                msg.MessageId,
                msg.UserId,
                msg.Username,
                msg.DisplayName,
                msg.Text,
                msg.ReceivedAt,
                isProtected,
                platform
            )
        );
    }

    /// <summary>Broadcaster, moderators and staff can't be deleted/muted/banned through the API, so no actions are offered on them (Twitch and Kick badge names).</summary>
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
                case "super_admin":
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
            _moderationLog.MarkAllDeleted(Platform.Twitch);
            if (ModerationMessagesVisible)
                RequestRender();
        });

    // Kick equivalents. Kick pushes these on the same chat channel as the messages themselves.
    private void OnKickMessageDeleted(string messageId) =>
        PostToUiThread(() =>
        {
            if (_moderationLog.MarkMessageDeleted(messageId) && ModerationMessagesVisible)
                RequestRender();
        });

    private void OnKickUserBanned(KickBanEvent ban) =>
        PostToUiThread(() =>
        {
            var state = ban.Permanent ? ModerationUserState.Banned : ModerationUserState.TimedOut;
            RecordKickSanction(ban.UserId, ban.Username, ban.Permanent, ban.ExpiresAtUtc);
            _moderationLog.MarkUserState(ban.UserId, state, Platform.Kick);
            if (ModerationMessagesVisible || _moderationTab == ModerationTab.Sanctions)
                RequestRender();
        });

    private void OnKickUserUnbanned(string userId) =>
        PostToUiThread(() =>
        {
            _kickSanctions.Remove(userId);
            _moderationLog.MarkUserState(userId, ModerationUserState.None, Platform.Kick);
            if (ModerationMessagesVisible || _moderationTab == ModerationTab.Sanctions)
                RequestRender();
        });

    private void OnKickChatCleared() =>
        PostToUiThread(() =>
        {
            _moderationLog.MarkAllDeleted(Platform.Kick);
            if (ModerationMessagesVisible)
                RequestRender();
        });

    /// <summary>
    /// Empties the Messages tab's log: all of it, or only one platform's rows (when that platform logs out, so the other
    /// chat's rows stay). The AutoMod queue is Twitch-only and goes with Twitch.
    /// </summary>
    private void ClearModerationLog(Platform? only = null)
    {
        if (only is { } platform)
            _moderationLog.ClearPlatform(platform);
        else
            _moderationLog.Clear();

        _moderationMessagesLastNewest = null;
        _moderationMessagesScroll = default;

        if (only is null or Platform.Twitch)
            _autoModQueue.Clear();
    }

    // ------------------------------------------------------------------ clicks

    private void SwitchModerationTab(ModerationTab tab)
    {
        if (tab == _moderationTab)
            return;

        CloseModerationDropdown();

        // The Chatters and AutoMod tabs share one top-anchored scroll state, so a stale offset must not carry over.
        if (
            tab is ModerationTab.AutoMod or ModerationTab.Sanctions
            || _moderationTab is ModerationTab.AutoMod or ModerationTab.Sanctions
        )
            _moderationScroll = default;

        _moderationTab = tab;
        _hoveredChatSettingButton = null;
        _moderationLoginButtonHovered = false;
        RequestRender();
    }

    private void HandleMessagesTabClick(int clientX, int clientY)
    {
        if (_moderationLinksChipRect is { } linksRect && Contains(linksRect, clientX, clientY))
        {
            _moderationLinksOnly = !_moderationLinksOnly;
            RequestRender();
            return;
        }

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
        OpenModerationDropdown(anchor, items);
    }

    private void SetModerationWindow(int minutes)
    {
        _settings.ModerationMessagesWindowMinutes = minutes;
        SettingsService.Save(_settings);
        _moderationMessagesScroll = default;
        _moderationMessagesLastNewest = null;
        RequestRender();
    }

    private void CopyMessageText(string text)
    {
        if (SetClipboardText(text))
            ShowSuccessToast(LocalizationService.T("Toast_Copied"));
        else
            ShowErrorToast(LocalizationService.T("Toast_CopyFailed"));
    }

    private void OpenMessageActionsDropdown(Rect anchor, ModerationLogEntry entry)
    {
        var items = new List<ModerationDropdownItem>
        {
            new()
            {
                Label = LocalizationService.T("Moderation_CopyMessage"),
                OnSelect = () => CopyMessageText(entry.Text),
            },
        };

        // The streamer's and other moderators' messages can't be moderated from here, but they can be copied.
        if (entry.IsProtected)
        {
            OpenModerationDropdown(anchor, items, MessageActionsDropdownWidth);
            return;
        }

        if (!entry.IsDeleted)
        {
            items.Add(
                new ModerationDropdownItem
                {
                    Label = LocalizationService.T("Moderation_ActDelete"),
                    NeedsKickDelete = true,
                    OnSelect = () => _ = RunMessageActionAsync(entry, true, ModerationSanction.None),
                }
            );
            if (entry.Platform != Platform.Kick) // Kick has no warnings
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
                    NeedsKickDelete = true,
                    NeedsKickBan = true,
                    OnSelect = () => OpenMessageMuteDurationDropdown(anchor, entry, deleteToo: true),
                }
            );
            items.Add(
                new ModerationDropdownItem
                {
                    Label = LocalizationService.T("Moderation_ActDeleteBan"),
                    NeedsKickDelete = true,
                    NeedsKickBan = true,
                    OnSelect = () => ConfirmBanFromMessage(entry, deleteToo: true),
                }
            );
        }

        items.Add(
            new ModerationDropdownItem
            {
                Label = LocalizationService.T("MainWindow_MuteMenu") + "  \u25b8",
                NeedsKickBan = true,
                OnSelect = () => OpenMessageMuteDurationDropdown(anchor, entry, deleteToo: false),
            }
        );
        if (entry.Platform != Platform.Kick) // Kick has no warnings
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
                NeedsKickBan = true,
                OnSelect = () => ConfirmBanFromMessage(entry, deleteToo: false),
            }
        );
        items.Add(
            new ModerationDropdownItem
            {
                Label = LocalizationService.T("Moderation_ActDeleteAllFromUser"),
                NeedsKickDelete = true,
                OnSelect = () => ConfirmDeleteAllFromUser(entry),
            }
        );

        ApplyKickScopeGate(items, entry.Platform);
        OpenModerationDropdown(anchor, items, MessageActionsDropdownWidth);
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
            // Kick's longest timeout is 7 days.
            if (entry.Platform == Platform.Kick && duration.Seconds > KickMaxTimeoutSeconds)
                continue;

            int seconds = duration.Seconds;
            items.Add(
                new ModerationDropdownItem
                {
                    Label = LocalizationService.T(duration.LabelKey),
                    NeedsKickBan = true,
                    NeedsKickDelete = deleteToo,
                    OnSelect = () => _ = RunMessageActionAsync(entry, deleteToo, ModerationSanction.Timeout, seconds),
                }
            );
        }

        ApplyKickScopeGate(items, entry.Platform);
        OpenModerationDropdown(anchor, items, MessageActionsDropdownWidth);
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
        if (!TryGetModerationActions(entry.Platform, out var moderation, out var channel))
            return;

        var platform = entry.Platform;
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

            // Twitch reports the sanction back through chat (CLEARCHAT); for Kick, tag the rows right away too, in
            // case the matching push event is slow or doesn't come. Marking is idempotent.
            if (platform == Platform.Kick && outcome.Sanction == true)
            {
                var state = sanction == ModerationSanction.Ban ? ModerationUserState.Banned : ModerationUserState.TimedOut;
                if (sanction is ModerationSanction.Ban or ModerationSanction.Timeout)
                {
                    _moderationLog.MarkUserState(entry.UserId, state, Platform.Kick);
                    RecordKickSanction(
                        entry.UserId,
                        name,
                        permanent: sanction == ModerationSanction.Ban,
                        sanction == ModerationSanction.Timeout ? DateTime.UtcNow.AddSeconds((int)timeoutSeconds) : null
                    );
                }
            }

            // Green when everything that was asked for worked, red as soon as one part failed.
            bool allOk = (outcome.Delete is null || outcome.Delete == ModerationDeleteResult.Ok) && outcome.Sanction != false;
            ReportModerationResult(BuildActionStatus(entry, name, sanction, outcome), allOk);
            RequestRender();
        });

        // The banned list on screen (Chatters tab) is Twitch's.
        if (platform == Platform.Twitch && outcome.Sanction == true && sanction is ModerationSanction.Timeout or ModerationSanction.Ban)
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
            parts.Add(DeleteStatus(delete, name, entry.Platform));

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

    private static string DeleteStatus(ModerationDeleteResult result, string name, Platform platform) =>
        result switch
        {
            ModerationDeleteResult.Ok => string.Format(LocalizationService.T("Moderation_MsgDeleted"), name),
            ModerationDeleteResult.MissingPermission => DeleteNeedsReloginText(platform),
            ModerationDeleteResult.Rejected => LocalizationService.T("Moderation_MsgDeleteRejected"),
            _ => string.Format(LocalizationService.T("Moderation_MsgDeleteFailed"), name),
        };

    private void ConfirmDeleteAllFromUser(ModerationLogEntry entry)
    {
        var ids = new List<string>();
        _moderationLog.CollectDeletableMessageIds(
            entry.UserId,
            DateTime.UtcNow.AddMinutes(-ModerationWindowMinutes),
            ids,
            entry.Platform
        );
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
                    _ = DeleteAllFromUserAsync(ids, name, entry.Platform);
            }
        );
    }

    private async Task DeleteAllFromUserAsync(List<string> ids, string name, Platform platform)
    {
        if (!TryGetModerationActions(platform, out var moderation, out var channel))
            return;

        _moderationStatusText = string.Format(LocalizationService.T("Moderation_Working"), name);
        RequestRender();

        var outcome = await ModerationActionRunner.DeleteManyAsync(
            moderation,
            channel,
            ids,
            onMessageDeleted: id => PostToUiThread(() => _moderationLog.MarkMessageDeleted(id))
        );

        // Queued after every per-message mark above (the UI queue is FIFO), so this single render shows them all.
        PostToUiThread(() =>
        {
            ReportModerationResult(
                outcome.MissingPermission
                    ? DeleteNeedsReloginText(platform)
                    : string.Format(LocalizationService.T("Moderation_MsgBulkResult"), outcome.Deleted, ids.Count, name),
                ok: !outcome.MissingPermission && outcome.Deleted == ids.Count
            );
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
                _moderationLog.MarkAllDeleted(Platform.Twitch); // Kick's public API has no clear-chat, so it is Twitch-only
                ReportModerationResult(LocalizationService.T("Moderation_ChatCleared"), ok: true);
            }
            else
            {
                ReportModerationResult(
                    result == ModerationDeleteResult.MissingPermission
                        ? LocalizationService.T("Moderation_DeleteNeedsRelogin")
                        : LocalizationService.T("Moderation_ClearChatFailed"),
                    ok: false
                );
            }
            RequestRender();
        });
    }

    private static string DisplayNameOf(ModerationLogEntry entry) =>
        string.IsNullOrEmpty(entry.DisplayName) ? entry.Login : entry.DisplayName;
}
