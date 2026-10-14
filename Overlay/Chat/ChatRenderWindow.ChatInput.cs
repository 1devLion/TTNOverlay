
using Vortice.Direct2D1;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the message box at the bottom of the main chat ([platform] [text] [Send]). It is only there
/// while the chat can be interacted with (title bar visible, so no click-through) and a logged-in platform can be written
/// to. The text, the keyboard handling and the sending live in ChatRenderWindow.Moderation.Stream.cs; the row itself is
/// drawn by DrawMessageInputRow.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private const float ChatInputStripPadding = 7f;

    /// <summary>True while the row was drawn this frame, so its hit areas are valid.</summary>
    private bool _chatInputRowShown;

    private float _chatInputStripShownHeight;

    private bool ChatInputAvailable =>
        !_showingModeration
        && !_showingEvents
        && !_clickThroughEnabled
        && !_bordersHidden
        && ((ModerationShowsTwitch && TwitchModerationReady) || (ModerationShowsKick && KickModerationReady));

    private float ChatInputStripHeightIfVisible() =>
        ChatInputAvailable ? ModerationInputRowHeight + ChatInputStripPadding * 2f : 0f;

    private void DrawChatInputStrip(ID2D1DCRenderTarget target, float width, float height, float strip)
    {
        _chatInputStripShownHeight = strip;

        if (strip <= 0f)
        {
            // Hidden (click-through, events view...): no hit areas, and the keyboard goes back to the app.
            _moderationInputFieldRect = null;
            _moderationSendRect = null;
            _moderationTargetRect = null;
            if (_moderationInput.IsFocused)
                _moderationInput.Blur();
            if (_dropdownOpen)
                CloseModerationDropdown();
            return;
        }

        EnsureModerationDrawingResources(target);

        float top = height - strip;
        target.FillRectangle(new Rect(0f, top, width, strip), _titleBarBrush);

        // Leaves the resize grip in the corner free.
        float rowWidth = Math.Max(120f, width - Padding * 2f - ResizeGripSize);
        bool both = TwitchModerationReady && ModerationShowsTwitch && KickModerationReady && ModerationShowsKick;
        DrawMessageInputRow(target, rowWidth, top + ChatInputStripPadding, showTargetPicker: both);
        _chatInputRowShown = true;
    }
}
