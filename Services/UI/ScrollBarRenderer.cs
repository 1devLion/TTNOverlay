using Vortice.Direct2D1;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Services;

/// <summary>
/// Draws the thin vertical scrollbar indicator (track + thumb) shared by every scrollable panel in the
/// app. Settings sections, the moderation panel, the chat/events message lists, release notes. Pure
/// rendering only: scroll math/state stays in <see cref="ScrollState"/>, wheel routing and content
/// clipping stay with each window (they already differ per caller, see DrawAlertsSection). This is
/// just the one piece that used to have no equivalent anywhere. Previously scrollable panels gave no
/// visual cue at all (or, in ReleaseNoteDialogWindow's case, a one-off "scroll for more" text hint).
/// Centralizing the actual bar means every caller gets the same look for free instead of redrawing the
/// geometry by hand.
/// </summary>
internal static class ScrollbarRenderer
{
    public const float Width = 4f;
    private const float MinThumbHeight = 24f;

    /// <summary>
    /// Draws a scrollbar for <paramref name="scroll"/> against <paramref name="viewport"/>. Pass the
    /// exact same rect used for the content's PushAxisAlignedClip. No-ops when there's nothing to
    /// scroll (Overflow &lt;= 0), so callers can call this unconditionally right after popping their
    /// content clip instead of guarding it themselves.
    /// </summary>
    public static void Draw(
        ID2D1DCRenderTarget target,
        Rect viewport,
        in ScrollState scroll,
        ID2D1SolidColorBrush trackBrush,
        ID2D1SolidColorBrush thumbBrush
    )
    {
        if (scroll.Overflow <= 0.5f || viewport.Height <= 0f)
            return;

        float contentHeight = viewport.Height + scroll.Overflow;
        float trackX = viewport.Right - Width;

        target.FillRectangle(new Rect(trackX, viewport.Top, Width, viewport.Height), trackBrush);

        float thumbHeight = System.Math.Min(
            viewport.Height,
            System.Math.Max(MinThumbHeight, viewport.Height * (viewport.Height / contentHeight))
        );
        float scrollableTrack = viewport.Height - thumbHeight;
        float thumbY = viewport.Top + scrollableTrack * (scroll.Offset / scroll.Overflow);

        target.FillRectangle(new Rect(trackX, thumbY, Width, thumbHeight), thumbBrush);
    }
}