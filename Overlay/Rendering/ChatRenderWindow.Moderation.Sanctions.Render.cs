using TTNOverlay.Models;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: draws the Sanctions tab, the list of banned / timed-out users of the platform(s) the panel
/// manages. Each row has the same three-dots button as the Chatters and Messages rows, which opens the unban/unmute menu.
/// Twitch rows come from the Helix banned list; Kick rows from what this app saw (see ChatRenderWindow.Moderation.KickSanctions.cs).
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private void DrawModerationSanctionsTab(
        ID2D1DCRenderTarget target,
        float width,
        float top,
        float visibleHeight,
        float maxWidth
    )
    {
        PruneKickSanctions();

        float totalHeight = MeasureOrDrawSanctions(target, maxWidth, 0f, draw: false);
        _moderationScroll.RecomputeOverflow(totalHeight, visibleHeight);

        float startY = top - _moderationScroll.Offset;
        target.PushAxisAlignedClip(new Rect(0f, top, width, visibleHeight), AntialiasMode.PerPrimitive);
        try
        {
            MeasureOrDrawSanctions(target, maxWidth, startY, draw: true);
        }
        finally
        {
            target.PopAxisAlignedClip();
        }
    }

    private List<SanctionRow> BuildSanctionRows()
    {
        var rows = new List<SanctionRow>();

        if (ModerationShowsTwitch && TwitchModerationReady && _moderationBanned is { } banned)
            foreach (var ban in banned)
                rows.Add(
                    new SanctionRow
                    {
                        Platform = Platform.Twitch,
                        Name = ban.Login,
                        Permanent = ban.ExpiresAt is null,
                        ExpiresAtUtc = ban.ExpiresAt,
                    }
                );

        if (ModerationShowsKick && KickModerationReady)
            foreach (var sanction in _kickSanctions.Values.OrderBy(s => s.Username, StringComparer.OrdinalIgnoreCase))
                rows.Add(
                    new SanctionRow
                    {
                        Platform = Platform.Kick,
                        Name = sanction.Username,
                        Permanent = sanction.Permanent || sanction.ExpiresAtUtc is null,
                        ExpiresAtUtc = sanction.ExpiresAtUtc,
                        Kick = sanction,
                    }
                );

        return rows;
    }

    private float MeasureOrDrawSanctions(ID2D1DCRenderTarget target, float maxWidth, float y, bool draw)
    {
        if (draw)
            _sanctionActionRects.Clear();

        float cursorY = y;

        if (!string.IsNullOrEmpty(_moderationStatusText) && !IsTwitchUnavailableStatus())
            cursorY = DrawModerationLine(target, _moderationStatusText, _moderationBodyFormat!, _moderationTextBrush!, maxWidth, cursorY, draw);

        // Logins needed for the platform(s) of the current mode.
        if (ModerationShowsTwitch && !TwitchModerationReady)
            cursorY = DrawModerationLine(
                target,
                LocalizationService.T("Moderation_MsgNeedLogin"),
                _moderationBodyFormat!,
                _moderationSecondaryBrush!,
                maxWidth,
                cursorY,
                draw
            );

        if (ModerationShowsKick)
        {
            if (!KickModerationReady)
                cursorY = DrawModerationLine(
                    target,
                    LocalizationService.T("Kick_MsgNeedLogin"),
                    _moderationBodyFormat!,
                    _moderationSecondaryBrush!,
                    maxWidth,
                    cursorY,
                    draw
                );
            else
            {
                // Kick can't list its banned users, so say what this list is made of.
                if (KickMissingBanScope is { } missing)
                    cursorY = DrawModerationLine(
                        target,
                        string.Format(LocalizationService.T("Kick_PermissionMissing"), missing),
                        _moderationBodyFormat!,
                        _moderationTextBrush!,
                        maxWidth,
                        cursorY,
                        draw
                    );

                cursorY = DrawModerationLine(
                    target,
                    LocalizationService.T("Kick_SanctionsNote"),
                    _moderationBodyFormat!,
                    _moderationSecondaryBrush!,
                    maxWidth,
                    cursorY,
                    draw
                );
            }
        }

        if (!ModerationHasUsablePlatform)
            return cursorY;

        cursorY += ModerationSectionSpacing;
        cursorY = DrawModerationLine(
            target,
            LocalizationService.T("MainWindow_ActiveSanctions"),
            _moderationHeaderFormat!,
            _moderationSecondaryBrush!,
            maxWidth,
            cursorY,
            draw
        );

        var rows = BuildSanctionRows();
        if (rows.Count == 0)
            return DrawModerationLine(
                target,
                LocalizationService.T("Kick_SanctionsEmpty"),
                _moderationBodyFormat!,
                _moderationSecondaryBrush!,
                maxWidth,
                cursorY,
                draw
            );

        // With both platforms in one list, the platform's logo (over a blank slot) says whose sanction it is.
        bool showIcons = ModerationMode == ModeMultichat;
        float textWidth = Math.Max(1f, maxWidth - ChatterActionButtonSize - ChatSettingCheckboxGap);

        foreach (var row in rows)
        {
            string expiration =
                row.Permanent || row.ExpiresAtUtc is null
                    ? LocalizationService.T("Moderation_Permanent")
                    : string.Format(
                        LocalizationService.T("Moderation_UntilDate"),
                        row.ExpiresAtUtc.Value.ToLocalTime().ToString("dd/MM HH:mm")
                    );

            float rowTop = cursorY;
            cursorY = DrawModerationLine(
                target,
                (showIcons ? ModerationIconSlot : "") + $"{row.Name} \u00b7 {expiration}",
                _moderationBodyFormat!,
                _moderationTextBrush!,
                textWidth,
                cursorY,
                draw
            );

            if (!draw)
                continue;

            if (showIcons)
                DrawModerationPlatformIconAt(target, row.Platform, Padding, rowTop);

            var buttonRect = new Rect(
                Padding + maxWidth - ChatterActionButtonSize,
                rowTop,
                ChatterActionButtonSize,
                Math.Min(cursorY - ModerationLineSpacing - rowTop, ChatterActionButtonSize)
            );
            DrawChatterActionButton(target, buttonRect);
            _sanctionActionRects.Add((buttonRect, row));
        }

        return cursorY;
    }
}
