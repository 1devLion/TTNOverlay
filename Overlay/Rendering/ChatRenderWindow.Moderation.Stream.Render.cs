using TTNOverlay.Overlay.Controls;
using System.Numerics;
using TTNOverlay.Models;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: drawing for the Kick account block of the Stream tab and for the message box of the
/// Messages tab (see ChatRenderWindow.Moderation.Stream.cs for the behavior).
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private ID2D1SolidColorBrush? _kickButtonFillBrush;
    private ID2D1SolidColorBrush? _kickButtonFillHoverBrush;
    private ID2D1SolidColorBrush? _kickButtonTextBrush;

    private ID2D1SolidColorBrush? _inputFieldBackgroundBrush;
    private ID2D1SolidColorBrush? _inputFieldBorderBrush;
    private ID2D1SolidColorBrush? _inputSelectionBrush;
    private bool? _inputBrushesWereDark;

    /// <summary>
    /// The Kick block of the Stream tab: who is logged in and the same login / log out button Twitch has. Returns the
    /// Y below it.
    /// </summary>
    private float DrawKickStreamSection(ID2D1DCRenderTarget target, float maxWidth, float cursorY, bool draw)
    {
        bool loggedIn = KickModerationReady;

        if (ModerationShowsTwitch)
        {
            cursorY += ModerationSectionSpacing;
            cursorY = DrawModerationLine(target, "Kick", _moderationHeaderFormat!, _moderationSecondaryBrush!, maxWidth, cursorY, draw);
        }

        string status = _moderationKickLoginBusy
            ? LocalizationService.T("Settings_KickApi_OpeningBrowser")
            : loggedIn
                ? string.Format(LocalizationService.T("Settings_KickApi_Connected"), _settings.KickLogin)
                : LocalizationService.T("Settings_KickApi_NotLoggedIn");
        cursorY = DrawModerationLine(target, status, _moderationBodyFormat!, _moderationTextBrush!, maxWidth, cursorY, draw);

        cursorY += ModerationLineSpacing;
        string label = LocalizationService.T(loggedIn ? "Common_Logout" : "Common_LoginWithKick");
        using var labelLayout = DWriteFactory.CreateTextLayout(
            label,
            GetOrCreateTwitchButtonFormat(),
            float.MaxValue,
            KickLoginButtonStyle.Height
        );
        var rect = KickLoginButtonStyle.Measure(labelLayout, Padding, cursorY);

        if (draw)
        {
            var icon = GetOrCreateLocalBadgeBitmap(target, "platform/kick", "platform/kick");
            if (!loggedIn)
            {
                bool dark = ThemeService.IsDark;
                _kickButtonFillBrush ??= target.CreateSolidColorBrush(KickLoginButtonStyle.PrimaryFill(dark));
                _kickButtonTextBrush ??= target.CreateSolidColorBrush(KickLoginButtonStyle.PrimaryText(dark));
                // The colors follow the theme, so they are set every frame instead of being baked into the brushes.
                _kickButtonFillBrush.Color = _moderationKickButtonHovered ? KickLoginButtonStyle.PrimaryFillHover(dark) : KickLoginButtonStyle.PrimaryFill(dark);
                _kickButtonTextBrush.Color = KickLoginButtonStyle.PrimaryText(dark);
                KickLoginButtonStyle.DrawPrimary(
                    target,
                    rect,
                    labelLayout,
                    _kickButtonFillBrush,
                    GetOrCreateTwitchButtonSecondaryBorderBrush(target),
                    _kickButtonTextBrush,
                    icon
                );
            }
            else
            {
                KickLoginButtonStyle.DrawSecondary(
                    target,
                    rect,
                    labelLayout,
                    GetOrCreateTwitchButtonSecondaryFillBrush(target, _moderationKickButtonHovered),
                    GetOrCreateTwitchButtonSecondaryBorderBrush(target),
                    GetOrCreateTwitchButtonSecondaryTextBrush(target),
                    icon
                );
            }
            _moderationKickActionRect = rect;
        }

        return rect.Bottom + ModerationLineSpacing;
    }

    /// <summary>
    /// Brushes of the message box and of the Kick login button of the Stream tab. They are created against the current render
    /// target, so they must be released whenever it is recreated (shrink-resize on leaving the panel) or the next
    /// frame draws with orphaned COM objects.
    /// </summary>
    private void DisposeMessageInputResources()
    {
        _inputFieldBackgroundBrush?.Dispose();
        _inputFieldBackgroundBrush = null;
        _inputFieldBorderBrush?.Dispose();
        _inputFieldBorderBrush = null;
        _inputSelectionBrush?.Dispose();
        _inputSelectionBrush = null;
        _kickButtonFillBrush?.Dispose();
        _kickButtonFillBrush = null;
        _kickButtonFillHoverBrush?.Dispose();
        _kickButtonFillHoverBrush = null;
        _kickButtonTextBrush?.Dispose();
        _kickButtonTextBrush = null;
    }

    /// <summary>
    /// The message box at the bottom of the main chat: [platform (Multichat only)] [text field] [Send].
    /// Records the hit areas for HandleModerationInputClick.
    /// </summary>
    private void DrawMessageInputRow(ID2D1DCRenderTarget target, float maxWidth, float rowTop, bool showTargetPicker)
    {
        if (InputBrushesThemeChanged(out bool dark))
        {
            _inputFieldBackgroundBrush?.Dispose();
            _inputFieldBackgroundBrush = null;
            _inputFieldBorderBrush?.Dispose();
            _inputFieldBorderBrush = null;
            _inputBrushesWereDark = dark;
        }
        _inputFieldBackgroundBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBackground);
        _inputFieldBorderBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBorder);
        _inputSelectionBrush ??= target.CreateSolidColorBrush(new Color4(0.30f, 0.62f, 0.98f, 0.35f));

        float left = Padding;
        float right = Padding + maxWidth;

        _moderationTargetRect = null;
        if (showTargetPicker)
        {
            string name = ModerationSendsToBoth ? ModeMultichat : ModerationSendPlatform == Platform.Kick ? "Kick" : "Twitch";
            var targetRect = DrawModerationPill(target, name + "  \u25be", left, rowTop);
            _moderationTargetRect = targetRect;
            left = targetRect.Right + ModerationPillGap;
        }

        bool canSend = !_moderationSending && _moderationInput.Text.Trim().Length > 0;
        string sendLabel = LocalizationService.T("Moderation_Send");
        float sendWidth = MeasureModerationPillWidth(sendLabel);
        if (!canSend)
            _moderationTextBrush!.Opacity = 0.45f;
        _suppressPillHover = !canSend; // a greyed-out Send does not light up
        var sendRect = DrawModerationPill(target, sendLabel, right - sendWidth, rowTop);
        _suppressPillHover = false;
        _moderationTextBrush!.Opacity = 1f;
        _moderationSendRect = sendRect;

        var field = new Rect(left, rowTop, Math.Max(40f, sendRect.Left - ModerationPillGap - left), sendRect.Height);
        _moderationInputFieldRect = field;

        DrawModerationTextField(target, _moderationInput, field, "Moderation_InputPlaceholder", "chat-input");
    }

    private void EnsureInputFieldBrushes(ID2D1DCRenderTarget target)
    {
        if (InputBrushesThemeChanged(out bool dark))
        {
            _inputFieldBackgroundBrush?.Dispose();
            _inputFieldBackgroundBrush = null;
            _inputFieldBorderBrush?.Dispose();
            _inputFieldBorderBrush = null;
            _inputBrushesWereDark = dark;
        }
        _inputFieldBackgroundBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBackground);
        _inputFieldBorderBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBorder);
        _inputSelectionBrush ??= target.CreateSolidColorBrush(new Color4(0.30f, 0.62f, 0.98f, 0.35f));
    }

    /// <summary>
    /// A single-line text field (background, border, hover, placeholder, selection, caret) for the message box and the
    /// user filter of the Messages tab.
    /// </summary>
    private void DrawModerationTextField(ID2D1DCRenderTarget target, TextBox box, Rect field, string placeholderKey, string hoverKey)
    {
        EnsureInputFieldBrushes(target);
        bool focused = box.IsFocused;
        target.FillRectangle(field, _inputFieldBackgroundBrush);
        target.DrawRectangle(field, focused ? _moderationTextBrush! : _inputFieldBorderBrush, focused ? 1.5f : 1f);

        float fieldHover = RegisterHover(field, hoverKey);
        if (fieldHover > 0f)
        {
            _moderationPillBrush!.Opacity = (ThemeService.IsDark ? 0.10f : 0.06f) * fieldHover;
            target.FillRectangle(field, _moderationPillBrush);
            _moderationPillBrush.Opacity = 1f;
            if (!focused)
            {
                _moderationSecondaryBrush!.Opacity = 0.6f * fieldHover;
                target.DrawRectangle(field, _moderationSecondaryBrush, 1f);
                _moderationSecondaryBrush.Opacity = 1f;
            }
        }

        float scroll = ModerationInputScroll(box, field, out float innerLeft, out string display);

        target.PushAxisAlignedClip(field, AntialiasMode.PerPrimitive);
        try
        {
            if (display.Length == 0 && !focused)
            {
                using var hint = DWriteFactory.CreateTextLayout(
                    LocalizationService.T(placeholderKey),
                    _moderationBodyFormat!,
                    4000f,
                    field.Height
                );
                target.DrawTextLayout(
                    new Vector2(innerLeft, field.Top + (field.Height - hint.Metrics.Height) / 2f),
                    hint,
                    _moderationSecondaryBrush!
                );
                return;
            }

            using var layout = DWriteFactory.CreateTextLayout(display, _moderationBodyFormat!, 4000f, field.Height);
            float textTop = field.Top + (field.Height - layout.Metrics.Height) / 2f;

            if (focused && box.HasSelection)
            {
                float selStartX = innerLeft + MeasureModerationInputWidth(display.Substring(0, box.SelectionStart)) - scroll;
                float selEndX = innerLeft + MeasureModerationInputWidth(display.Substring(0, box.SelectionEnd)) - scroll;
                target.FillRectangle(
                    new Rect(selStartX, field.Top + 3f, selEndX - selStartX, field.Height - 6f),
                    _inputSelectionBrush
                );
            }

            target.DrawTextLayout(new Vector2(innerLeft - scroll, textTop), layout, _moderationTextBrush!);

            if (focused && box.CaretVisibleThisFrame && !box.HasSelection)
            {
                float caretX =
                    innerLeft
                    + MeasureModerationInputWidth(display.Substring(0, Math.Min(box.CaretIndex, display.Length)))
                    - scroll;
                target.DrawLine(
                    new Vector2(caretX, field.Top + 5f),
                    new Vector2(caretX, field.Bottom - 5f),
                    _moderationTextBrush!,
                    1.5f
                );
            }
        }
        finally
        {
            target.PopAxisAlignedClip();
        }
    }

    /// <summary>True (with the new value) when the theme changed since the field brushes were created.</summary>
    private bool InputBrushesThemeChanged(out bool dark)
    {
        dark = ThemeService.IsDark;
        return _inputBrushesWereDark != dark;
    }
}
