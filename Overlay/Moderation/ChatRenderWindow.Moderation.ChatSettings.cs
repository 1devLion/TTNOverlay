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

