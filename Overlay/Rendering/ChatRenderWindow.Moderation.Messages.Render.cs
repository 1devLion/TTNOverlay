using System.Numerics;
using TTNOverlay.Models;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: Direct2D drawing for the moderation tab strip and the Messages tab.
/// The tab strip and the controls row are fixed; only the message list scrolls, anchored to the newest message like
/// the main chat. Per-frame cost is bounded: row heights are cached per entry, at most
/// <see cref="MaxModerationRowMeasuresPerFrame"/> rows are measured in one frame (newest first), and only the rows
/// that are on screen get a text layout created and drawn.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private const int MaxModerationRowMeasuresPerFrame = 150;
    private const float ModerationMessageRowSpacing = 6f;
    private const float ModerationEstimatedRowHeight = 20f;
    private const float ModerationPillPaddingX = 12f;
    private const float ModerationPillPaddingY = 5f;
    private const float ModerationPillGap = 8f;

    private ScrollState _moderationMessagesScroll;
    private ModerationLogEntry? _moderationMessagesLastNewest;

    // Reused every frame instead of allocating a list per render.
    private float[] _moderationRowHeights = new float[256];

    private readonly List<ModerationLogEntry> _moderationVisibleRows = new();

    private const string ModerationIconSlot = "\u2003\u2002";

    // Same box height for both logos' row; like the title bar, Twitch's glyph fills its canvas and kick.webp has
    // transparent padding, so Kick is drawn bigger to look the same. Tweak here if one looks off.
    private const float ModerationTwitchIconSize = 14f;
    private const float ModerationKickIconSize = 17f;

    private float _moderationTimePrefixWidth;
    private float _moderationLineHeight;

    /// <summary>Where the logo goes: the width of "HH:mm:ss  " in the body font (digits are tabular, so it is the same for every row).</summary>
    private void EnsureModerationIconMetrics()
    {
        if (_moderationTimePrefixWidth > 0f)
            return;

        using var prefix = DWriteFactory.CreateTextLayout("00:00:00  ", _moderationBodyFormat!, float.MaxValue, 1000f);
        _moderationTimePrefixWidth = prefix.Metrics.WidthIncludingTrailingWhitespace;
        _moderationLineHeight = prefix.Metrics.Height;
    }

    private void DrawModerationPlatformIcon(ID2D1DCRenderTarget target, Platform platform, float rowY)
    {
        EnsureModerationIconMetrics();
        DrawModerationPlatformIconAt(target, platform, Padding + _moderationTimePrefixWidth, rowY);
    }

    /// <summary>Draws a platform logo at x, vertically centered on the first text line of a row starting at rowY.</summary>
    private void DrawModerationPlatformIconAt(ID2D1DCRenderTarget target, Platform platform, float x, float rowY)
    {
        EnsureModerationIconMetrics();

        string key = platform == Platform.Kick ? "platform/kick" : "platform/twitch";
        var icon = GetOrCreateLocalBadgeBitmap(target, key, key);
        if (icon is null)
            return;

        float size = platform == Platform.Kick ? ModerationKickIconSize : ModerationTwitchIconSize;
        DrawBitmapAt(target, icon, x, rowY + (_moderationLineHeight - size) / 2f, size);
    }

    /// <summary>Draws the fixed tab strip (Chatters / Messages / AutoMod) and returns the Y just below it. Tabs that don't fit the width continue on a second row.</summary>
    private float DrawModerationTabStrip(ID2D1DCRenderTarget target, float top, float maxWidth)
    {
        _moderationTabRects.Clear();
        _moderationModeRects.Clear();

        // Which platform(s) the panel manages: a pill with the current mode that opens a Multichat / Kick / Twitch list.
        float x = Padding;
        float rowTop = top;
        float bottom = top;

        var modeRect = DrawModerationPill(target, ModerationMode + "  \u25be", Padding, top);
        _moderationModeRects.Add((modeRect, ModerationMode));
        bottom = modeRect.Bottom;

        // Tabs on their own row under the mode selector.
        x = Padding;
        rowTop = bottom + 6f;
        bottom = rowTop;

        foreach (var (tab, labelKey) in ModerationTabs)
        {
            // AutoMod is a Twitch feature; Stream, Messages and Sanctions cover both platforms.
            if (tab is ModerationTab.AutoMod && !ModerationShowsTwitch)
                continue;

            using var layout = DWriteFactory.CreateTextLayout(
                ModerationTabLabel(tab, labelKey),
                _moderationHeaderFormat!,
                float.MaxValue,
                1000f
            );
            float pillWidth = layout.Metrics.WidthIncludingTrailingWhitespace + ModerationPillPaddingX * 2f;

            if (x > Padding && x + pillWidth > Padding + maxWidth)
            {
                x = Padding;
                rowTop = bottom + 6f;
            }

            var rect = new Rect(
                x,
                rowTop,
                pillWidth,
                layout.Metrics.Height + ModerationPillPaddingY * 2f
            );

            bool active = tab == _moderationTab;
            DrawPillBackground(target, rect, active ? (ThemeService.IsDark ? 0.24f : 0.14f) : (ThemeService.IsDark ? 0.08f : 0.05f), active ? 0.6f : 0.3f);
            target.DrawTextLayout(
                new Vector2(rect.Left + ModerationPillPaddingX, rect.Top + ModerationPillPaddingY),
                layout,
                active ? _moderationTextBrush! : _moderationSecondaryBrush!
            );

            _moderationTabRects.Add((rect, tab));
            x = rect.Right + ModerationPillGap;
            bottom = Math.Max(bottom, rect.Bottom);
        }

        return bottom + ModerationSectionSpacing * 0.75f;
    }

    private void DrawPillBackground(ID2D1DCRenderTarget target, Rect rect, float fillOpacity, float borderOpacity)
    {
        // Tabs and pills: the fill gets stronger while the mouse is over them (disabled ones do not react).
        if (!_suppressPillHover)
        {
            float hover = RegisterHover(
                rect,
                (object)((int)MathF.Round(rect.Left), (int)MathF.Round(rect.Top), (int)MathF.Round(rect.Width), (int)MathF.Round(rect.Height))
            );
            fillOpacity += (ThemeService.IsDark ? 0.12f : 0.07f) * hover;
            borderOpacity = Math.Min(1f, borderOpacity + 0.25f * hover);
        }

        var rounded = new RoundedRectangle
        {
            Rect = rect,
            RadiusX = ChatSettingPillCornerRadius,
            RadiusY = ChatSettingPillCornerRadius,
        };

        _moderationPillBrush!.Opacity = fillOpacity;
        target.FillRoundedRectangle(rounded, _moderationPillBrush);
        _moderationPillBrush.Opacity = 1f;

        _moderationSecondaryBrush!.Opacity = borderOpacity;
        target.DrawRoundedRectangle(rounded, _moderationSecondaryBrush, 1f);
        _moderationSecondaryBrush.Opacity = 1f;
    }

    private float MeasureModerationPillWidth(string text)
    {
        using var layout = DWriteFactory.CreateTextLayout(text, _moderationBodyFormat!, float.MaxValue, 1000f);
        return layout.Metrics.WidthIncludingTrailingWhitespace + ModerationPillPaddingX * 2f;
    }

    private Rect DrawModerationPill(ID2D1DCRenderTarget target, string text, float x, float y, bool active = false)
    {
        using var layout = DWriteFactory.CreateTextLayout(text, _moderationBodyFormat!, float.MaxValue, 1000f);
        var rect = new Rect(
            x,
            y,
            layout.Metrics.WidthIncludingTrailingWhitespace + ModerationPillPaddingX * 2f,
            layout.Metrics.Height + ModerationPillPaddingY * 2f
        );

        float idleFill = ThemeService.IsDark ? 0.12f : 0.07f;
        DrawPillBackground(target, rect, active ? idleFill + 0.14f : idleFill, active ? 0.8f : 0.35f);
        target.DrawTextLayout(
            new Vector2(rect.Left + ModerationPillPaddingX, rect.Top + ModerationPillPaddingY),
            layout,
            _moderationTextBrush!
        );
        return rect;
    }

    private void DrawModerationMessagesTab(
        ID2D1DCRenderTarget target,
        float width,
        float top,
        float visibleHeight,
        float maxWidth
    )
    {
        bool twitchReady = TwitchModerationReady && ModerationShowsTwitch;
        bool kickReady = KickModerationReady && ModerationShowsKick;
        bool needsLogin = !twitchReady && !kickReady;

        // Sin sesión, el estado general ("Iniciá sesión con la cuenta de moderador para gestionar el chat") dice
        // lo mismo que el aviso específico de esta pestaña: se muestra solo este último. Si hay sesión de Kick pero
        // no de Twitch, el aviso de Twitch (login o integración desactivada) tampoco corresponde acá.
        bool hideGeneralStatus = !twitchReady && (kickReady ? IsTwitchUnavailableStatus() : IsModerationLoginPromptStatus());
        float y = hideGeneralStatus
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
            y = DrawModerationLine(
                target,
                LocalizationService.T("Moderation_MsgNeedLogin"),
                _moderationBodyFormat!,
                _moderationSecondaryBrush!,
                maxWidth,
                y,
                draw: true
            );

            // Kick's login lives in Settings, not in the Chatters tab, so say so when the panel is on Kick.
            if (ModerationShowsKick)
                DrawModerationLine(
                    target,
                    LocalizationService.T("Kick_MsgNeedLogin"),
                    _moderationBodyFormat!,
                    _moderationSecondaryBrush!,
                    maxWidth,
                    y,
                    draw: true
                );
            return;
        }

        // Kick lets the user untick permissions: say which ones are missing (the menu items needing them are greyed out).
        if (kickReady)
            foreach (var missingScope in KickMissingModerationScopes())
                y = DrawModerationLine(
                    target,
                    string.Format(LocalizationService.T("Kick_PermissionMissing"), missingScope),
                    _moderationBodyFormat!,
                    _moderationTextBrush!,
                    maxWidth,
                    y,
                    draw: true
                );

        y = DrawMessagesControlsRow(target, maxWidth, y + 2f, showClearChat: twitchReady);

        float listHeight = top + visibleHeight - y;
        if (listHeight > 0f)
            DrawModerationMessagesList(target, width, maxWidth, y, listHeight);
    }

    private float DrawMessagesControlsRow(ID2D1DCRenderTarget target, float maxWidth, float y, bool showClearChat)
    {
        string windowLabel =
            string.Format(LocalizationService.T("Moderation_MsgWindowLabel"), ModerationWindowMinutes) + " \u25be";
        string clearLabel = LocalizationService.T("Moderation_ClearChat");

        var windowRect = DrawModerationPill(target, windowLabel, Padding, y);
        _moderationWindowPillRect = windowRect;

        // Links: shows everything that was sent with a link (from everybody, or from the user in the search box).
        var linksRect = DrawModerationPill(
            target,
            (_moderationLinksOnly ? "\u2713 " : "") + LocalizationService.T("Moderation_FilterLinks"),
            windowRect.Right + ModerationPillGap,
            y,
            active: _moderationLinksOnly
        );
        _moderationLinksChipRect = linksRect;
        float rowBottom = Math.Max(windowRect.Bottom, linksRect.Bottom);

        // Clearing the whole chat only exists on Twitch (Kick's public API has no such call).
        if (showClearChat)
        {
            // Right-aligned when it fits next to the other pills; otherwise it drops to its own row.
            float clearWidth = MeasureModerationPillWidth(clearLabel);
            float clearX = Padding + maxWidth - clearWidth;
            float clearY = y;
            if (clearX < linksRect.Right + ModerationPillGap)
            {
                clearX = Padding;
                clearY = rowBottom + 6f;
            }

            var clearRect = DrawModerationPill(target, clearLabel, clearX, clearY);
            _moderationClearChatPillRect = clearRect;
            rowBottom = Math.Max(rowBottom, clearRect.Bottom);
        }

        // Search box (nick), under the pills.
        var filterRect = new Rect(Padding, rowBottom + 8f, maxWidth, windowRect.Height);
        _moderationFilterFieldRect = filterRect;
        DrawModerationTextField(target, _moderationFilterInput, filterRect, "Moderation_FilterPlaceholder", "filter-input");

        if (_moderationFilterInput.Text.Length > 0)
        {
            using var xLayout = DWriteFactory.CreateTextLayout("\u2715", _moderationBodyFormat!, 100f, filterRect.Height);
            var clearBox = new Rect(filterRect.Right - 28f, filterRect.Top, 28f, filterRect.Height);
            _moderationFilterClearRect = clearBox;
            float clearHover = RegisterHover(clearBox, "filter-clear");
            _moderationSecondaryBrush!.Opacity = 0.6f + 0.4f * clearHover;
            target.DrawTextLayout(
                new Vector2(clearBox.Left + (clearBox.Width - (float)xLayout.Metrics.Width) / 2f, filterRect.Top + (filterRect.Height - (float)xLayout.Metrics.Height) / 2f),
                xLayout,
                _moderationSecondaryBrush
            );
            _moderationSecondaryBrush.Opacity = 1f;
        }

        return filterRect.Bottom + 8f;
    }

    private void DrawModerationMessagesList(
        ID2D1DCRenderTarget target,
        float width,
        float maxWidth,
        float listTop,
        float listHeight
    )
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(ModerationWindowMinutes);
        int firstIndex = _moderationLog.FirstIndexAtOrAfter(cutoff);

        // Only the rows of the platform(s) this panel mode manages that match the filter, oldest first.
        string nickFilter = ModerationFilterNick;
        bool filtering = _moderationLinksOnly || nickFilter.Length > 0;

        // A different filter starts at the newest message again.
        string filterKey = (_moderationLinksOnly ? "1|" : "0|") + nickFilter.ToLowerInvariant();
        if (!string.Equals(filterKey, _moderationFilterApplied, StringComparison.Ordinal))
        {
            _moderationFilterApplied = filterKey;
            _moderationMessagesScroll = default;
            _moderationMessagesLastNewest = null;
        }

        var rows = _moderationVisibleRows;
        rows.Clear();
        if (filtering)
        {
            for (int n = firstIndex; n < _moderationLog.Count; n++)
            {
                var candidate = _moderationLog[n];
                if (ModerationShowsPlatform(candidate.Platform) && EntryMatchesModerationFilter(candidate, nickFilter))
                    rows.Add(candidate);
            }
        }

        int first = 0;
        int count = rows.Count;

        if (count <= 0)
        {
            _moderationMessagesLastNewest = null;
            _moderationMessagesScroll = default;
            DrawModerationLine(
                target,
                LocalizationService.T(filtering ? "Moderation_FilterNoResults" : "Moderation_FilterHint"),
                _moderationBodyFormat!,
                _moderationSecondaryBrush!,
                maxWidth,
                listTop,
                draw: true
            );
            return;
        }

        float textWidth = Math.Max(1f, maxWidth - ChatterActionButtonSize - ChatSettingCheckboxGap);

        if (_moderationRowHeights.Length < count)
            Array.Resize(ref _moderationRowHeights, Math.Max(count, _moderationRowHeights.Length * 2));

        // Newest first, so if the per-frame budget runs out it is the oldest (off-screen) rows that keep an
        // estimated height for a frame or two, never the ones the user is looking at.
        int budget = MaxModerationRowMeasuresPerFrame;
        bool incomplete = false;
        for (int i = count - 1; i >= 0; i--)
            _moderationRowHeights[i] = GetModerationRowHeight(
                rows[first + i],
                textWidth,
                ref budget,
                ref incomplete
            );

        float total = 0f;
        float addedAtBottom = 0f;
        var lastNewest = _moderationMessagesLastNewest;
        bool pastLastNewest = lastNewest is null;
        for (int i = 0; i < count; i++)
        {
            float step = _moderationRowHeights[i] + ModerationMessageRowSpacing;
            total += step;
            if (pastLastNewest)
                addedAtBottom += step;
            else if (ReferenceEquals(rows[first + i], lastNewest))
                pastLastNewest = true;
        }
        total -= ModerationMessageRowSpacing;

        // Same bottom-anchored behavior as the chat: new messages push the view only when it is already at the
        // bottom; if the moderator scrolled up to read, the content stays put.
        _moderationMessagesScroll.OnContentGrew(addedAtBottom);
        _moderationMessagesLastNewest = rows[first + count - 1];
        _moderationMessagesScroll.RecomputeOverflow(total, listHeight);

        float overflow = Math.Max(0f, total - listHeight);
        float startY = listTop - overflow + _moderationMessagesScroll.Offset;
        float listBottom = listTop + listHeight;

        SetLinkClip(listTop, listBottom);
        target.PushAxisAlignedClip(new Rect(0f, listTop, width, listHeight), AntialiasMode.PerPrimitive);
        try
        {
            float cursorY = startY;
            for (int i = 0; i < count; i++)
            {
                if (cursorY >= listBottom)
                    break;

                float height = _moderationRowHeights[i];
                if (cursorY + height >= listTop)
                    DrawModerationMessageRow(
                        target,
                        rows[first + i],
                        cursorY,
                        height,
                        textWidth,
                        maxWidth,
                        listTop,
                        listBottom
                    );
                cursorY += height + ModerationMessageRowSpacing;
            }
        }
        finally
        {
            target.PopAxisAlignedClip();
            SetLinkClip(0f, float.MaxValue);
        }

        if (incomplete)
            PostToUiThread(RequestRender);
    }

    /// <summary>
    /// A row with a link that can't be laid out falls back to the plain text layout (for good: the entry is marked as
    /// having no link) instead of failing the whole frame, which would leave the panel blank.
    /// </summary>
    private bool TryMeasureLinkRow(ModerationLogEntry entry, float textWidth, out float height)
    {
        try
        {
            height = FlowModerationRow(null, entry, 0f, 0f, textWidth, draw: false);
            return true;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("ChatRenderWindow.TryMeasureLinkRow", ex);
            entry.LinkState = 1;
            height = 0f;
            return false;
        }
    }

    private bool TryDrawLinkRow(ID2D1DCRenderTarget target, ModerationLogEntry entry, float y, float textWidth)
    {
        try
        {
            FlowModerationRow(target, entry, Padding, y, textWidth, draw: true);
            return true;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("ChatRenderWindow.TryDrawLinkRow", ex);
            entry.LinkState = 1;
            entry.InvalidateLayout();
            return false;
        }
    }

    private float GetModerationRowHeight(
        ModerationLogEntry entry,
        float textWidth,
        ref int budget,
        ref bool incomplete
    )
    {
        if (entry.CachedVersion == _moderationLayoutVersion && entry.CachedWidth == textWidth)
            return entry.CachedHeight;

        if (budget <= 0)
        {
            incomplete = true;
            return entry.CachedHeight > 0f ? entry.CachedHeight : ModerationEstimatedRowHeight;
        }

        budget--;
        if (ChatLinks.HasLink(entry) && TryMeasureLinkRow(entry, textWidth, out float linkRowHeight))
        {
            // Rows with a link are laid out word by word (blue links), see FlowModerationRow.
            entry.CachedHeight = linkRowHeight;
        }
        else
        {
            using var layout = CreateModerationRowLayout(entry, textWidth);
            entry.CachedHeight = layout.Metrics.Height;
        }
        entry.CachedWidth = textWidth;
        entry.CachedVersion = _moderationLayoutVersion;
        return entry.CachedHeight;
    }

    /// <summary>One row as a single text layout: "HH:mm:ss  Name: text [tags]", with the name in bold.</summary>
    private IDWriteTextLayout CreateModerationRowLayout(ModerationLogEntry entry, float textWidth)
    {
        entry.TimeText ??= entry.ReceivedAtUtc.ToLocalTime().ToString("HH:mm:ss");
        string name = DisplayNameOf(entry);

        // With both chats mixed in one list, the platform's logo (drawn over this blank slot) says which one the row
        // belongs to. The slot is an em space plus an en space, about 1.5x the font size, wide enough for the logo.
        string tag = ModerationMode == ModeMultichat ? ModerationIconSlot : "";

        string text = $"{entry.TimeText}  {tag}{name}: {entry.Text}";
        if (entry.IsDeleted)
            text += " " + LocalizationService.T("Moderation_MsgDeletedTag");
        if (entry.UserState == ModerationUserState.TimedOut)
            text += " " + LocalizationService.T("Moderation_MsgMutedTag");
        else if (entry.UserState == ModerationUserState.Banned)
            text += " " + LocalizationService.T("Moderation_MsgBannedTag");

        var layout = DWriteFactory.CreateTextLayout(text, _moderationBodyFormat!, textWidth, 4000f);
        layout.SetFontWeight(
            Vortice.DirectWrite.FontWeight.Bold,
            new Vortice.DirectWrite.TextRange((uint)(entry.TimeText.Length + 2 + tag.Length), (uint)(name.Length + 1))
        );
        return layout;
    }

    private void DrawModerationMessageRow(
        ID2D1DCRenderTarget target,
        ModerationLogEntry entry,
        float y,
        float height,
        float textWidth,
        float maxWidth,
        float listTop,
        float listBottom
    )
    {
        // Hover: the whole row gets a light fill, behind the text.
        var rowBounds = new Rect(Padding - 6f, y, maxWidth + 12f, height);
        float rowHover = RegisterHover(rowBounds, entry, listTop, listBottom);
        if (rowHover > 0f)
        {
            _moderationPillBrush!.Opacity = (ThemeService.IsDark ? 0.08f : 0.05f) * rowHover;
            target.FillRectangle(rowBounds, _moderationPillBrush);
            _moderationPillBrush.Opacity = 1f;
        }

        if (ChatLinks.HasLink(entry) && TryDrawLinkRow(target, entry, y, textWidth))
        {
            // drawn word by word, see FlowModerationRow
        }
        else
        {
            using var layout = CreateModerationRowLayout(entry, textWidth);

            // Deleted messages stay visible (the moderator may still want to act on the author) but dimmed.
            target.DrawTextLayout(
                new Vector2(Padding, y),
                layout,
                entry.IsDeleted ? _moderationSecondaryBrush! : _moderationTextBrush!
            );
        }

        if (ModerationMode == ModeMultichat)
            DrawModerationPlatformIcon(target, entry.Platform, y);

        var buttonRect = new Rect(
            Padding + maxWidth - ChatterActionButtonSize,
            y,
            ChatterActionButtonSize,
            Math.Min(height, ChatterActionButtonSize)
        );
        DrawChatterActionButton(target, buttonRect);

        // The clickable area is clipped to the list, so a row scrolled underneath the fixed controls above can't be
        // triggered by clicking on them.
        float hitTop = Math.Max(buttonRect.Top, listTop);
        float hitBottom = Math.Min(buttonRect.Bottom, listBottom);
        if (hitBottom > hitTop)
            _moderationMessageActionRects.Add(
                (new Rect(buttonRect.Left, hitTop, buttonRect.Width, hitBottom - hitTop), entry)
            );
    }
}
