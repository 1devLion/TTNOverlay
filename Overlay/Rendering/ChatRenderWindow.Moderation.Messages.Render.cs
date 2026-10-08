using System.Numerics;
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

    /// <summary>Draws the fixed tab strip (Chatters / Messages / AutoMod) and returns the Y just below it. Tabs that don't fit the width continue on a second row.</summary>
    private float DrawModerationTabStrip(ID2D1DCRenderTarget target, float top, float maxWidth)
    {
        _moderationTabRects.Clear();

        float x = Padding;
        float rowTop = top;
        float bottom = top;

        foreach (var (tab, labelKey) in ModerationTabs)
        {
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

    private Rect DrawModerationPill(ID2D1DCRenderTarget target, string text, float x, float y)
    {
        using var layout = DWriteFactory.CreateTextLayout(text, _moderationBodyFormat!, float.MaxValue, 1000f);
        var rect = new Rect(
            x,
            y,
            layout.Metrics.WidthIncludingTrailingWhitespace + ModerationPillPaddingX * 2f,
            layout.Metrics.Height + ModerationPillPaddingY * 2f
        );

        DrawPillBackground(target, rect, ThemeService.IsDark ? 0.12f : 0.07f, 0.35f);
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
        bool needsLogin = _moderation is not { HasCredentials: true, IsLoggedIn: true };

        // Sin sesión, el estado general ("Iniciá sesión con la cuenta de moderador para gestionar el chat") dice
        // lo mismo que el aviso específico de esta pestaña: se muestra solo este último.
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
                LocalizationService.T("Moderation_MsgNeedLogin"),
                _moderationBodyFormat!,
                _moderationSecondaryBrush!,
                maxWidth,
                y,
                draw: true
            );
            return;
        }

        y = DrawMessagesControlsRow(target, maxWidth, y + 2f);

        float listHeight = top + visibleHeight - y;
        if (listHeight > 0f)
            DrawModerationMessagesList(target, width, maxWidth, y, listHeight);
    }

    private float DrawMessagesControlsRow(ID2D1DCRenderTarget target, float maxWidth, float y)
    {
        string windowLabel =
            string.Format(LocalizationService.T("Moderation_MsgWindowLabel"), ModerationWindowMinutes) + " \u25be";
        string clearLabel = LocalizationService.T("Moderation_ClearChat");

        var windowRect = DrawModerationPill(target, windowLabel, Padding, y);
        _moderationWindowPillRect = windowRect;

        // Right-aligned when it fits next to the window pill; otherwise it drops to its own row.
        float clearWidth = MeasureModerationPillWidth(clearLabel);
        float clearX = Padding + maxWidth - clearWidth;
        float clearY = y;
        if (clearX < windowRect.Right + ModerationPillGap)
        {
            clearX = Padding;
            clearY = windowRect.Bottom + 6f;
        }

        var clearRect = DrawModerationPill(target, clearLabel, clearX, clearY);
        _moderationClearChatPillRect = clearRect;

        return Math.Max(windowRect.Bottom, clearRect.Bottom) + 8f;
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
        int first = _moderationLog.FirstIndexAtOrAfter(cutoff);
        int count = _moderationLog.Count - first;

        if (count <= 0)
        {
            _moderationMessagesLastNewest = null;
            _moderationMessagesScroll = default;
            DrawModerationLine(
                target,
                LocalizationService.T("Moderation_MsgEmpty"),
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
                _moderationLog[first + i],
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
            else if (ReferenceEquals(_moderationLog[first + i], lastNewest))
                pastLastNewest = true;
        }
        total -= ModerationMessageRowSpacing;

        // Same bottom-anchored behavior as the chat: new messages push the view only when it is already at the
        // bottom; if the moderator scrolled up to read, the content stays put.
        _moderationMessagesScroll.OnContentGrew(addedAtBottom);
        _moderationMessagesLastNewest = _moderationLog[first + count - 1];
        _moderationMessagesScroll.RecomputeOverflow(total, listHeight);

        float overflow = Math.Max(0f, total - listHeight);
        float startY = listTop - overflow + _moderationMessagesScroll.Offset;
        float listBottom = listTop + listHeight;

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
                        _moderationLog[first + i],
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
        }

        if (incomplete)
            PostToUiThread(RequestRender);
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
        using var layout = CreateModerationRowLayout(entry, textWidth);
        entry.CachedHeight = layout.Metrics.Height;
        entry.CachedWidth = textWidth;
        entry.CachedVersion = _moderationLayoutVersion;
        return entry.CachedHeight;
    }

    /// <summary>One row as a single text layout: "HH:mm:ss  Name: text [tags]", with the name in bold.</summary>
    private IDWriteTextLayout CreateModerationRowLayout(ModerationLogEntry entry, float textWidth)
    {
        entry.TimeText ??= entry.ReceivedAtUtc.ToLocalTime().ToString("HH:mm:ss");
        string name = DisplayNameOf(entry);

        string text = $"{entry.TimeText}  {name}: {entry.Text}";
        if (entry.IsDeleted)
            text += " " + LocalizationService.T("Moderation_MsgDeletedTag");
        if (entry.UserState == ModerationUserState.TimedOut)
            text += " " + LocalizationService.T("Moderation_MsgMutedTag");
        else if (entry.UserState == ModerationUserState.Banned)
            text += " " + LocalizationService.T("Moderation_MsgBannedTag");

        var layout = DWriteFactory.CreateTextLayout(text, _moderationBodyFormat!, textWidth, 4000f);
        layout.SetFontWeight(
            Vortice.DirectWrite.FontWeight.Bold,
            new Vortice.DirectWrite.TextRange((uint)(entry.TimeText.Length + 2), (uint)(name.Length + 1))
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
        using (var layout = CreateModerationRowLayout(entry, textWidth))
        {
            // Deleted messages stay visible (the moderator may still want to act on the author) but dimmed.
            target.DrawTextLayout(
                new Vector2(Padding, y),
                layout,
                entry.IsDeleted ? _moderationSecondaryBrush! : _moderationTextBrush!
            );
        }

        if (entry.IsProtected)
            return;

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
