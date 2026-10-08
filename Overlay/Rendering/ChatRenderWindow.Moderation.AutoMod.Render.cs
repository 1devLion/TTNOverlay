using TTNOverlay.Services;
using TTNOverlay.Twitch;
using Vortice.Direct2D1;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: Direct2D drawing for the moderation panel's AutoMod tab. The list is short (the queue
/// is capped), so it is measured and drawn in two passes like the Chatters tab, scrolling with the shared
/// top-anchored <c>_moderationScroll</c>. Each held message is a header line, a reason line and a row of
/// Allow/Deny pills on their own line, so nothing depends on the overlay being wide.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private const float AutoModItemSpacing = 10f;

    /// <summary>True when the general status line is just the "log in with the moderator account" prompt.</summary>
    private bool IsModerationLoginPromptStatus() =>
        string.Equals(_moderationStatusText, LocalizationService.T("Moderation_LoginPrompt"), StringComparison.Ordinal);

    /// <summary>Tab label; the AutoMod tab shows how many messages are waiting.</summary>
    private string ModerationTabLabel(ModerationTab tab, string labelKey) =>
        tab == ModerationTab.AutoMod && _autoModQueue.Count > 0
            ? string.Format(LocalizationService.T("Moderation_TabAutoModCount"), _autoModQueue.Count)
            : LocalizationService.T(labelKey);

    private void DrawModerationAutoModTab(
        ID2D1DCRenderTarget target,
        float width,
        float top,
        float visibleHeight,
        float maxWidth
    )
    {
        bool needsLogin = _moderation is not { HasCredentials: true, IsLoggedIn: true };

        float y = needsLogin && IsModerationLoginPromptStatus()
            ? top
            : DrawModerationLine(
                target,
                _moderationStatusText,
                _moderationBodyFormat!,
                _moderationTextBrush!,
                maxWidth,
                top,
                draw: true
            );

        if (needsLogin)
        {
            DrawModerationLine(
                target,
                LocalizationService.T("Moderation_AutoModNeedLogin"),
                _moderationBodyFormat!,
                _moderationSecondaryBrush!,
                maxWidth,
                y,
                draw: true
            );
            return;
        }

        float listTop = y + 2f;
        float listHeight = top + visibleHeight - listTop;
        if (listHeight <= 0f)
            return;

        if (_autoModQueue.Count == 0)
        {
            // An empty list only means "nothing is held" while the connection is actually up.
            string key = _autoModNeedsRelogin
                ? "Moderation_AutoModUnavailable"
                : _autoModSubscribed
                    ? "Moderation_AutoModEmpty"
                    : "Moderation_AutoModConnecting";
            DrawModerationLine(
                target,
                LocalizationService.T(key),
                _moderationBodyFormat!,
                _moderationSecondaryBrush!,
                maxWidth,
                listTop,
                draw: true
            );
            return;
        }

        float total = MeasureOrDrawAutoModItems(target, maxWidth, 0f, draw: false);
        _moderationScroll.RecomputeOverflow(total, listHeight);

        float startY = listTop - _moderationScroll.Offset;
        float listBottom = listTop + listHeight;

        target.PushAxisAlignedClip(new Rect(0f, listTop, width, listHeight), AntialiasMode.PerPrimitive);
        try
        {
            MeasureOrDrawAutoModItems(target, maxWidth, startY, draw: true, listTop, listBottom);
        }
        finally
        {
            target.PopAxisAlignedClip();
        }
    }

    /// <summary>Returns the total height of the queue. With <paramref name="draw"/> it also paints it and records the click areas.</summary>
    private float MeasureOrDrawAutoModItems(
        ID2D1DCRenderTarget target,
        float maxWidth,
        float startY,
        bool draw,
        float listTop = 0f,
        float listBottom = 0f
    )
    {
        string allowLabel = "\u2713  " + LocalizationService.T("Moderation_AutoModAllow");
        string denyLabel = "\u2715  " + LocalizationService.T("Moderation_AutoModDeny");
        float pillHeight = MeasureModerationPillHeight(allowLabel);

        float y = startY;
        for (int i = 0; i < _autoModQueue.Count; i++)
        {
            var message = _autoModQueue[i];
            string time = message.HeldAtUtc.ToLocalTime().ToString("HH:mm:ss");

            y = DrawModerationLine(
                target,
                $"{time}  {DisplayNameOf(message)}: {message.Text}",
                _moderationBodyFormat!,
                _moderationTextBrush!,
                maxWidth,
                y,
                draw
            );
            y = DrawModerationLine(
                target,
                AutoModReasonText(message),
                _moderationBodyFormat!,
                _moderationSecondaryBrush!,
                maxWidth,
                y,
                draw
            );

            if (draw)
            {
                var allowRect = DrawModerationPill(target, allowLabel, Padding, y);
                var denyRect = DrawModerationPill(target, denyLabel, allowRect.Right + ModerationPillGap, y);

                // Hit areas are clipped to the list so a row scrolled underneath the fixed controls above can't be
                // triggered through them, and none are offered while a request for that message is in flight.
                if (!message.IsBusy)
                {
                    AddAutoModHitArea(allowRect, message, allow: true, listTop, listBottom);
                    AddAutoModHitArea(denyRect, message, allow: false, listTop, listBottom);
                }
            }

            y += pillHeight + AutoModItemSpacing;
        }

        return y - startY;
    }

    private void AddAutoModHitArea(Rect rect, AutoModHeldMessage message, bool allow, float listTop, float listBottom)
    {
        float hitTop = Math.Max(rect.Top, listTop);
        float hitBottom = Math.Min(rect.Bottom, listBottom);
        if (hitBottom > hitTop)
            _moderationAutoModActionRects.Add(
                (new Rect(rect.Left, hitTop, rect.Width, hitBottom - hitTop), message, allow)
            );
    }

    private float MeasureModerationPillHeight(string text)
    {
        using var layout = DWriteFactory.CreateTextLayout(text, _moderationBodyFormat!, float.MaxValue, 1000f);
        return layout.Metrics.Height + ModerationPillPaddingY * 2f;
    }

    private static string AutoModReasonText(AutoModHeldMessage message)
    {
        if (message.Reason == AutoModHoldReason.BlockedTerm)
            return LocalizationService.T("Moderation_AutoModReasonBlockedTerm");

        if (!string.IsNullOrEmpty(message.Category))
        {
            string category = message.Category.Replace('_', ' ');
            return message.Level is { } level
                ? string.Format(LocalizationService.T("Moderation_AutoModReasonCategoryLevel"), category, level)
                : string.Format(LocalizationService.T("Moderation_AutoModReasonCategory"), category);
        }

        return LocalizationService.T("Moderation_AutoModReasonGeneric");
    }
}
