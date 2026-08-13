using TTNOverlay.Services;
using TTNOverlay.Twitch;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: shows/hides the moderation panel and tracks its open/closed state.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private ModerationService? _moderation;

    private bool? _bordersHiddenBeforeModeration;
    private int? _normalWidthBeforeModeration;
    private int? _normalHeightBeforeModeration;

    private void ToggleModerationPanel()
    {
        if (!_settings.EnableModerationPanel)
            return;

        StopAutoRevertTimer();

        if (_showingModeration)
        {
            SetView(showEvents: false);
            return;
        }

        _moderation ??= new ModerationService(_settings);
        SetView(showEvents: false, showModeration: true);
        _ = RefreshModerationStateAsync();
    }

    private void EnterModerationLayout()
    {

        _bordersHiddenBeforeModeration = _bordersHidden;
        if (_bordersHidden)
            SetBordersHidden(false);

        if (Win32.GetWindowRect(Hwnd, out var rect))
        {
            _normalWidthBeforeModeration = rect.Right - rect.Left;
            _normalHeightBeforeModeration = rect.Bottom - rect.Top;
        }

        Resize((int)_settings.ModerationWindowWidth, (int)_settings.ModerationWindowHeight);
    }

    private void ExitModerationLayout()
    {
        CloseModerationDropdown();

        _moderationChatters = new();
        _moderationBanned = null;
        _moderationChatSettings = null;
        _moderationChatterRowRects.Clear();
        _moderationBannedRowRects.Clear();
        _moderationChatSettingRowRects.Clear();
        _moderationChatSettingButtonRects.Clear();

        if (Win32.GetWindowRect(Hwnd, out var currentRect))
        {
            _settings.ModerationWindowWidth = currentRect.Right - currentRect.Left;
            _settings.ModerationWindowHeight = currentRect.Bottom - currentRect.Top;
        }

        if (_bordersHiddenBeforeModeration.HasValue)
        {
            SetBordersHidden(_bordersHiddenBeforeModeration.Value);
            _bordersHiddenBeforeModeration = null;
        }

        if (_normalWidthBeforeModeration.HasValue && _normalHeightBeforeModeration.HasValue)
        {
            Resize(_normalWidthBeforeModeration.Value, _normalHeightBeforeModeration.Value);
            _normalWidthBeforeModeration = null;
            _normalHeightBeforeModeration = null;
        }
    }

    private void DisconnectModeration() => _moderation = null;
}
