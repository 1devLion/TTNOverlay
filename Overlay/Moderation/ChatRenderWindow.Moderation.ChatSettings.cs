using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using TTNOverlay.Services;
using TTNOverlay.Twitch;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the chat-settings tab of the moderation panel (slow mode, follower-only, etc.).
/// </summary>
internal sealed partial class ChatRenderWindow
{

    private void HandleChatSettingCheckboxClick(ModerationChatSettingField field)
    {
        if (_moderationChatSettings is not { } current)
            return;

        var updated = CloneChatSettings(current);
        switch (field)
        {
            case ModerationChatSettingField.Subscriber:
                updated.SubscriberMode = !current.SubscriberMode;
                break;
            case ModerationChatSettingField.Emote:
                updated.EmoteMode = !current.EmoteMode;
                break;
            case ModerationChatSettingField.Unique:
                updated.UniqueChatMode = !current.UniqueChatMode;
                break;
            default:
                return;
        }
        _ = SaveChatSettingsAsync(updated);
    }

    private static HelixClient.ChatSettings CloneChatSettings(HelixClient.ChatSettings source) =>
        new()
        {
            EmoteMode = source.EmoteMode,
            FollowerMode = source.FollowerMode,
            FollowerModeDurationMinutes = source.FollowerModeDurationMinutes,
            SlowMode = source.SlowMode,
            SlowModeWaitSeconds = source.SlowModeWaitSeconds,
            SubscriberMode = source.SubscriberMode,
            UniqueChatMode = source.UniqueChatMode,
        };

    private void OpenChatSettingDurationDropdown(Rect anchor, ModerationChatSettingField field)
    {
        _dropdownOwnerChatSettingField = field;
        if (_moderationChatSettings is not { } current)
            return;

        var items = new List<ModerationDropdownItem>();

        bool isOn = field == ModerationChatSettingField.Follower ? current.FollowerMode : current.SlowMode;
        if (isOn)
        {
            items.Add(
                new ModerationDropdownItem
                {
                    Label = LocalizationService.T("Moderation_TurnOffLabel"),
                    OnSelect = () =>
                    {
                        var off = CloneChatSettings(current);
                        if (field == ModerationChatSettingField.Follower)
                            off.FollowerMode = false;
                        else
                            off.SlowMode = false;
                        _ = SaveChatSettingsAsync(off);
                    },
                }
            );
        }

        if (field == ModerationChatSettingField.Follower)
        {
            foreach (var duration in ModerationFollowerDurations)
            {
                int minutes = duration.Minutes;
                items.Add(
                    new ModerationDropdownItem
                    {
                        Label = LocalizationService.T(duration.LabelKey),
                        OnSelect = () =>
                        {
                            var updated = CloneChatSettings(current);
                            updated.FollowerMode = true;
                            updated.FollowerModeDurationMinutes = minutes;
                            _ = SaveChatSettingsAsync(updated);
                        },
                    }
                );
            }
        }
        else
        {
            foreach (var duration in ModerationSlowDurations)
            {
                int seconds = duration.Seconds;
                items.Add(
                    new ModerationDropdownItem
                    {
                        Label = LocalizationService.T(duration.LabelKey),
                        OnSelect = () =>
                        {
                            var updated = CloneChatSettings(current);
                            updated.SlowMode = true;
                            updated.SlowModeWaitSeconds = seconds;
                            _ = SaveChatSettingsAsync(updated);
                        },
                    }
                );
            }
        }

        OpenModerationDropdown(anchor.Left, anchor.Bottom, items);
    }

    /// <summary>
    /// ROOMSTATE: someone (another moderator, the streamer, Twitch's own UI) changed a chat mode, or the full state
    /// arrived after joining. Keeps the checkboxes in step with Twitch while the panel is open; ignored when the
    /// settings haven't been loaded yet (the Helix request that loads them is the source of truth then).
    /// </summary>
    private void OnIrcRoomState(IrcRoomStateUpdate update) =>
        PostToUiThread(() =>
        {
            if (_moderationChatSettings is not { } current)
                return;

            var next = CloneChatSettings(current);
            if (update.EmoteOnly is { } emoteOnly)
                next.EmoteMode = emoteOnly;
            if (update.SubsOnly is { } subsOnly)
                next.SubscriberMode = subsOnly;
            if (update.UniqueChat is { } uniqueChat)
                next.UniqueChatMode = uniqueChat;
            if (update.SlowSeconds is { } slow)
            {
                next.SlowMode = slow > 0;
                next.SlowModeWaitSeconds = slow > 0 ? slow : null;
            }
            if (update.FollowersOnlyMinutes is { } followers)
            {
                // -1 means off; 0 means on with no minimum follow time (same shape Helix reports).
                next.FollowerMode = followers >= 0;
                next.FollowerModeDurationMinutes = followers >= 0 ? followers : null;
            }

            if (ChatSettingsEqual(current, next))
                return;

            _moderationChatSettings = next;
            if (_showingModeration)
                RequestRender();
        });

    private static bool ChatSettingsEqual(HelixClient.ChatSettings a, HelixClient.ChatSettings b) =>
        a.EmoteMode == b.EmoteMode
        && a.SubscriberMode == b.SubscriberMode
        && a.UniqueChatMode == b.UniqueChatMode
        && a.SlowMode == b.SlowMode
        && a.SlowModeWaitSeconds == b.SlowModeWaitSeconds
        && a.FollowerMode == b.FollowerMode
        && a.FollowerModeDurationMinutes == b.FollowerModeDurationMinutes;

    private async Task SaveChatSettingsAsync(HelixClient.ChatSettings updated)
    {
        if (_moderation is null)
            return;

        _moderationStatusText = LocalizationService.T("Moderation_SavingChatSettings");
        RequestRender();

        var ok = await _moderation.UpdateChatSettingsAsync(_settings.Channel, updated);

        PostToUiThread(() =>
        {
            _moderationStatusText = LocalizationService.T(
                ok ? "Moderation_ChatSettingsSaved" : "Moderation_ChatSettingsSaveFailed"
            );
            if (ok)
                _moderationChatSettings = updated;
            RequestRender();
        });
    }
}

