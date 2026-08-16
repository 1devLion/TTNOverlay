using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using TTNOverlay.Services;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: Direct2D drawing for the moderation panel.
/// </summary>
internal sealed partial class ChatRenderWindow
{

    private void DrawModerationPanel(
        ID2D1DCRenderTarget target,
        float width,
        float height,
        float top,
        float visibleHeight
    )
    {
        _moderationTextBrush ??= target.CreateSolidColorBrush(ThemeService.OverlayText);
        _moderationPillBrush ??= target.CreateSolidColorBrush(ThemeService.PureContrastTint);
        _moderationSecondaryBrush ??= target.CreateSolidColorBrush(ThemeService.WindowTextSecondary);

        _moderationHeaderFormat ??= CreateTitleBarFormat(
            "Segoe UI",
            Vortice.DirectWrite.FontWeight.Bold,
            12.5f,
            Vortice.DirectWrite.TextAlignment.Leading
        );

        _moderationHeaderFormat.ParagraphAlignment = ParagraphAlignment.Near;
        _moderationBodyFormat ??= CreateTitleBarFormat("Segoe UI", Vortice.DirectWrite.FontWeight.Normal, 14.5f, Vortice.DirectWrite.TextAlignment.Leading);
        _moderationBodyFormat.ParagraphAlignment = ParagraphAlignment.Near;

        float maxWidth = width - Padding * 2;
        if (maxWidth <= 0)
            return;

        float totalHeight = MeasureOrDrawModerationContent(target, maxWidth, 0f, draw: false);
        _moderationScroll.RecomputeOverflow(totalHeight, visibleHeight);

        float startY = top - _moderationScroll.Offset;

        target.PushAxisAlignedClip(new Rect(0f, top, width, visibleHeight), AntialiasMode.PerPrimitive);
        try
        {
            MeasureOrDrawModerationContent(target, maxWidth, startY, draw: true);
        }
        finally
        {
            target.PopAxisAlignedClip();
        }
    }

    private float MeasureOrDrawModerationContent(
        ID2D1DCRenderTarget target,
        float maxWidth,
        float y,
        bool draw
    )
    {

        if (draw)
        {
            _moderationChatterRowRects.Clear();
            _moderationBannedRowRects.Clear();
            _moderationChatSettingRowRects.Clear();
            _moderationChatSettingButtonRects.Clear();
        }

        float cursorY = y;
        cursorY = DrawModerationHeaderSection(target, maxWidth, cursorY, draw);
        cursorY = DrawChatSettingsSection(target, maxWidth, cursorY, draw);
        cursorY = DrawChattersSection(target, maxWidth, cursorY, draw);
        return cursorY;
    }

    private float DrawModerationHeaderSection(ID2D1DCRenderTarget target, float maxWidth, float cursorY, bool draw)
    {
        cursorY = DrawModerationLine(
            target,
            _moderationStatusText,
            _moderationBodyFormat!,
            _moderationTextBrush!,
            maxWidth,
            cursorY,
            draw
        );
        if (!string.IsNullOrEmpty(_moderationCountText))
            cursorY = DrawModerationLine(
                target,
                _moderationCountText,
                _moderationBodyFormat!,
                _moderationSecondaryBrush!,
                maxWidth,
                cursorY,
                draw
            );

        if (draw)
        {
            _moderationLoginActionRect = null;
            _moderationRefreshActionRect = null;
        }
        if (_moderation is { HasCredentials: true })
        {
            bool isLogin = !_moderation.IsLoggedIn;
            string actionLabel = LocalizationService.T(
                isLogin ? "Common_LoginWithTwitch" : "Common_Logout"
            );

            cursorY += ModerationLineSpacing;
            float actionRowTop = cursorY;

            using var actionLabelLayout = DWriteFactory.CreateTextLayout(
                actionLabel,
                GetOrCreateTwitchButtonFormat(),
                float.MaxValue,
                TwitchLoginButtonStyle.Height
            );
            var actionRect = TwitchLoginButtonStyle.Measure(actionLabelLayout, Padding, actionRowTop);

            if (draw)
            {
                if (isLogin)
                    TwitchLoginButtonStyle.DrawPrimary(
                        target,
                        actionRect,
                        actionLabelLayout,
                        GetOrCreateTwitchButtonPrimaryFillBrush(target, _moderationLoginButtonHovered),
                        GetOrCreateTwitchButtonTextBrush(target),
                        GetOrCreateTwitchButtonIconBitmap(target, TwitchIconLoader.Variant.White)
                    );
                else
                    TwitchLoginButtonStyle.DrawSecondary(
                        target,
                        actionRect,
                        actionLabelLayout,
                        GetOrCreateTwitchButtonSecondaryFillBrush(target, _moderationLoginButtonHovered),
                        GetOrCreateTwitchButtonSecondaryBorderBrush(target),
                        GetOrCreateTwitchButtonSecondaryTextBrush(target),
                        GetOrCreateTwitchButtonIconBitmap(target, ThemeService.IsDark ? TwitchIconLoader.Variant.White : TwitchIconLoader.Variant.Dark)
                    );

                _moderationLoginActionRect = actionRect;
                _moderationLoginActionIsLogin = isLogin;
            }

            cursorY = actionRect.Bottom + ModerationLineSpacing;

            if (!isLogin)
            {
                float refreshRowTop = cursorY;
                cursorY = DrawModerationLine(
                    target,
                    LocalizationService.T("MainWindow_RefreshWithIcon"),
                    _moderationBodyFormat!,
                    _moderationTextBrush!,
                    maxWidth,
                    cursorY,
                    draw
                );
                if (draw)
                    _moderationRefreshActionRect = new Rect(
                        Padding,
                        refreshRowTop,
                        maxWidth,
                        cursorY - ModerationLineSpacing - refreshRowTop
                    );
            }
        }

        return cursorY;
    }

    private float DrawChatSettingsSection(ID2D1DCRenderTarget target, float maxWidth, float cursorY, bool draw)
    {
        if (_moderationChatSettings is not { } chatSettings)
            return cursorY;

        cursorY += ModerationSectionSpacing;
        cursorY = DrawModerationLine(
            target,
            LocalizationService.T("MainWindow_ChatSettingsHeader"),
            _moderationHeaderFormat!,
            _moderationSecondaryBrush!,
            maxWidth,
            cursorY,
            draw
        );

        cursorY = DrawChatSettingToggleRow(
            target,
            maxWidth,
            cursorY,
            draw,
            ModerationChatSettingField.Subscriber,
            chatSettings.SubscriberMode,
            LocalizationService.T("MainWindow_SubscribersOnly"),
            checkboxInteractive: true,
            hasMenuButton: false
        );
        cursorY = DrawChatSettingToggleRow(
            target,
            maxWidth,
            cursorY,
            draw,
            ModerationChatSettingField.Emote,
            chatSettings.EmoteMode,
            LocalizationService.T("MainWindow_EmotesOnly"),
            checkboxInteractive: true,
            hasMenuButton: false
        );
        cursorY = DrawChatSettingToggleRow(
            target,
            maxWidth,
            cursorY,
            draw,
            ModerationChatSettingField.Unique,
            chatSettings.UniqueChatMode,
            LocalizationService.T("MainWindow_UniqueChat"),
            checkboxInteractive: true,
            hasMenuButton: false
        );

        cursorY = DrawChatSettingToggleRow(
            target,
            maxWidth,
            cursorY,
            draw,
            ModerationChatSettingField.Follower,
            chatSettings.FollowerMode,
            chatSettings.FollowerMode
                ? string.Format(
                    LocalizationService.T("Moderation_ChatSettingFollowerOnLabel"),
                    chatSettings.FollowerModeDurationMinutes ?? 0
                )
                : LocalizationService.T("MainWindow_FollowersOnly"),
            checkboxInteractive: false,
            hasMenuButton: true
        );
        cursorY = DrawChatSettingToggleRow(
            target,
            maxWidth,
            cursorY,
            draw,
            ModerationChatSettingField.Slow,
            chatSettings.SlowMode,
            chatSettings.SlowMode
                ? string.Format(
                    LocalizationService.T("Moderation_ChatSettingSlowOnLabel"),
                    chatSettings.SlowModeWaitSeconds ?? 30
                )
                : LocalizationService.T("MainWindow_SlowMode"),
            checkboxInteractive: false,
            hasMenuButton: true
        );

        return cursorY;
    }

    private float DrawChattersSection(ID2D1DCRenderTarget target, float maxWidth, float cursorY, bool draw)
    {
        if (_moderationChatters.Count > 0)
        {
            cursorY += ModerationSectionSpacing;
            cursorY = DrawModerationLine(
                target,
                LocalizationService.T("MainWindow_ConnectedChatters"),
                _moderationHeaderFormat!,
                _moderationSecondaryBrush!,
                maxWidth,
                cursorY,
                draw
            );
            float chatterNameMaxWidth = Math.Max(1f, maxWidth - ChatterActionButtonSize - ChatSettingCheckboxGap);
            foreach (var chatter in _moderationChatters)
            {
                float rowTop = cursorY;
                cursorY = DrawModerationLine(
                    target,
                    chatter.Login,
                    _moderationBodyFormat!,
                    _moderationTextBrush!,
                    chatterNameMaxWidth,
                    cursorY,
                    draw
                );
                float rowBottom = cursorY - ModerationLineSpacing;
                if (draw)
                {
                    using var nameLayout = DWriteFactory.CreateTextLayout(chatter.Login, _moderationBodyFormat!, chatterNameMaxWidth, 1000f);
                    float desiredX = Padding + nameLayout.Metrics.WidthIncludingTrailingWhitespace + ChatSettingCheckboxGap;
                    float maxX = Padding + maxWidth - ChatterActionButtonSize;
                    var buttonRect = new Rect(
                        Math.Min(desiredX, maxX),
                        rowTop,
                        ChatterActionButtonSize,
                        rowBottom - rowTop
                    );
                    DrawChatterActionButton(target, buttonRect);
                    _moderationChatterRowRects.Add((buttonRect, chatter.Id, chatter.Login));
                }
            }
        }

        if (_moderationBanned is { Count: > 0 })
        {
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
            foreach (var row in _moderationBanned)
            {
                string expiration =
                    row.ExpiresAt is null
                        ? LocalizationService.T("Moderation_Permanent")
                        : string.Format(
                            LocalizationService.T("Moderation_UntilDate"),
                            row.ExpiresAt.Value.ToLocalTime().ToString("dd/MM HH:mm")
                        );
                float rowTop = cursorY;
                cursorY = DrawModerationLine(
                    target,
                    $"{row.Login} \u2014 {expiration}",
                    _moderationBodyFormat!,
                    _moderationTextBrush!,
                    maxWidth,
                    cursorY,
                    draw
                );
                if (draw)
                    _moderationBannedRowRects.Add(
                        (
                            new Rect(Padding, rowTop, maxWidth, cursorY - ModerationLineSpacing - rowTop),
                            row.Login,
                            row.ExpiresAt is null
                        )
                    );
            }
        }

        return cursorY;
    }

    private float DrawModerationLine(
        ID2D1DCRenderTarget target,
        string text,
        IDWriteTextFormat format,
        ID2D1SolidColorBrush brush,
        float maxWidth,
        float y,
        bool draw
    )
    {
        if (string.IsNullOrEmpty(text))
            return y;

        using var layout = DWriteFactory.CreateTextLayout(text, format, maxWidth, 1000f);
        float lineHeight = layout.Metrics.Height;

        if (draw)
            target.DrawTextLayout(new Vector2(Padding, y), layout, brush);

        return y + lineHeight + ModerationLineSpacing;
    }

    private const float ChatSettingCheckboxSize = 12f;
    private const float ChatSettingCheckboxGap = 8f;

    private float DrawChatSettingToggleRow(
        ID2D1DCRenderTarget target,
        float maxWidth,
        float y,
        bool draw,
        ModerationChatSettingField field,
        bool isOn,
        string label,
        bool checkboxInteractive,
        bool hasMenuButton
    )
    {
        float buttonReserve = hasMenuButton ? ChatSettingMenuButtonWidth + ChatSettingCheckboxGap : 0f;
        float labelX = Padding + ChatSettingCheckboxSize + ChatSettingCheckboxGap;
        float labelMaxWidth = Math.Max(1f, maxWidth - ChatSettingCheckboxSize - ChatSettingCheckboxGap - buttonReserve);

        using var layout = DWriteFactory.CreateTextLayout(label, _moderationBodyFormat!, labelMaxWidth, 1000f);
        float lineHeight = layout.Metrics.Height;
        float rowTop = y;

        if (draw)
        {
            float boxTop = y + (lineHeight - ChatSettingCheckboxSize) / 2f;
            var boxRect = new Rect(Padding, boxTop, ChatSettingCheckboxSize, ChatSettingCheckboxSize);
            if (isOn)
                target.FillRectangle(boxRect, _moderationTextBrush!);
            else
                target.DrawRectangle(boxRect, _moderationTextBrush!, 1.5f);

            Rect buttonRect = default;
            Rect pillRect = default;
            if (hasMenuButton)
            {
                float desiredX = labelX + layout.Metrics.WidthIncludingTrailingWhitespace + ChatSettingCheckboxGap;
                float maxX = Padding + maxWidth - ChatSettingMenuButtonWidth;
                buttonRect = new Rect(Math.Min(desiredX, maxX), rowTop, ChatSettingMenuButtonWidth, lineHeight);
                pillRect = new Rect(
                    labelX - ChatSettingPillPaddingX,
                    rowTop - ChatSettingPillPaddingY,
                    buttonRect.Right - (labelX - ChatSettingPillPaddingX),
                    lineHeight + ChatSettingPillPaddingY * 2
                );

                float hoverProgress = field == ModerationChatSettingField.Follower
                    ? _followerButtonHoverProgress
                    : _slowButtonHoverProgress;
                bool isActive = _dropdownOpen && _dropdownOwnerChatSettingField == field;
                float progress = isActive ? 1f : hoverProgress;
                float idleAlpha = ThemeService.IsDark ? 0.10f : 0.06f;
                float hoverAlpha = ThemeService.IsDark ? 0.22f : 0.13f;

                var roundedPill = new RoundedRectangle
                {
                    Rect = pillRect,
                    RadiusX = ChatSettingPillCornerRadius,
                    RadiusY = ChatSettingPillCornerRadius,
                };
                _moderationPillBrush!.Opacity = idleAlpha + (hoverAlpha - idleAlpha) * progress;
                target.FillRoundedRectangle(roundedPill, _moderationPillBrush);
                _moderationPillBrush.Opacity = 1f;

                _moderationSecondaryBrush!.Opacity = 0.35f;
                target.DrawRoundedRectangle(roundedPill, _moderationSecondaryBrush, 1f);
                _moderationSecondaryBrush.Opacity = 1f;
            }

            target.DrawTextLayout(new Vector2(labelX, y), layout, _moderationTextBrush!);

            if (checkboxInteractive)
            {
                const float hitPadding = 6f;
                _moderationChatSettingRowRects.Add((
                    new Rect(
                        Padding - hitPadding,
                        boxTop - hitPadding,
                        ChatSettingCheckboxSize + hitPadding * 2,
                        ChatSettingCheckboxSize + hitPadding * 2
                    ),
                    field
                ));
            }

            if (hasMenuButton)
            {
                DrawChatSettingMenuButton(target, buttonRect);

                _moderationChatSettingButtonRects.Add((pillRect, field));
            }
        }

        return y + lineHeight + ModerationLineSpacing + (hasMenuButton ? ChatSettingPillPaddingY * 2f : 0f);
    }

    private void DrawChatSettingMenuButton(ID2D1DCRenderTarget target, Rect bounds)
    {
        using var layout = DWriteFactory.CreateTextLayout("▾", _moderationBodyFormat!, bounds.Width, bounds.Height);
        target.DrawTextLayout(new Vector2(bounds.Left, bounds.Top), layout, _moderationSecondaryBrush!);
    }

    private void DrawChatterActionButton(ID2D1DCRenderTarget target, Rect bounds)
    {
        float cx = bounds.Left + bounds.Width / 2f;
        float cy = bounds.Top + bounds.Height / 2f;
        const float dotRadius = 1.6f;
        const float dotGap = 5f;
        for (int i = -1; i <= 1; i++)
        {
            var center = new Vector2(cx, cy + i * dotGap);
            target.FillEllipse(new Ellipse(center, dotRadius, dotRadius), _moderationSecondaryBrush!);
        }
    }
}