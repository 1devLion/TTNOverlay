using System.Numerics;
using TTNOverlay.Native;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: drawing and hit-testing for the custom title bar and its buttons.
/// </summary>
internal sealed partial class ChatRenderWindow
{

    private void DrawTitleBar(ID2D1DCRenderTarget target, float width)
    {
        _titleBarForegroundBrush ??= target.CreateSolidColorBrush(ThemeService.OverlayText);
        _titleBarButtonFormat ??= CreateTitleBarFormat(
            "Segoe UI Symbol",
            FontWeight.Bold,
            17f,
            TextAlignment.Center
        );
        _titleBarLabelFormat ??= CreateTitleBarFormat(
            "Segoe UI",
            FontWeight.Bold,
            14f,
            TextAlignment.Leading
        );

        _titleBarHoverBrush ??= target.CreateSolidColorBrush(ThemeService.PureContrastTint);
        _closeHoverBrush ??= target.CreateSolidColorBrush(new Color4(0.85f, 0.16f, 0.16f, 1f));

        var (settingsRect, bordersRect, closeRect) = GetTitleBarButtonRects(width, TitleBarHeight);

        DrawTitleBarHoverBackground(target, settingsRect, _titleBarHoverBrush, _settingsHoverProgress, 0.14f);
        DrawTitleBarHoverBackground(target, bordersRect, _titleBarHoverBrush, _bordersHoverProgress, 0.14f);
        DrawTitleBarHoverBackground(target, closeRect, _closeHoverBrush, _closeHoverProgress, 1f);

        DrawTitleBarSymbol(target, settingsRect, "\u2699");
        DrawTitleBarSymbol(target, bordersRect, "\u26F6");
        DrawTitleBarSymbol(target, closeRect, "\u2715");

        float labelX = settingsRect.Right + 4f;
        float labelWidth = bordersRect.Left - labelX;
        if (labelWidth > 0)
        {
            var labelClipRect = new Rect(labelX, 0f, labelWidth, TitleBarHeight);
            target.PushAxisAlignedClip(labelClipRect, AntialiasMode.Aliased);

            if (_twitchActive && _kickActive)
            {
                // Both sources active (Multichat): "Twitch: conectado  |  Kick: conectado" doesn't
                // fit comfortably and repeats itself. Two small status dots carry the same
                // information (connecting/connected/error per source) far more compactly.
                DrawConnectionDots(target, labelX, labelWidth);
            }
            else
            {
                using var labelLayout = DWriteFactory.CreateTextLayout(
                    _connectionStatusText,
                    _titleBarLabelFormat,
                    labelWidth,
                    TitleBarHeight
                );
                target.DrawTextLayout(new Vector2(labelX, 0f), labelLayout, _titleBarForegroundBrush);
            }

            target.PopAxisAlignedClip();
        }
    }

    private const float ConnectionDotRadius = 4f;
    private const float ConnectionDotGap = 6f; // between a dot and its letter
    private const float ConnectionDotGroupGap = 14f; // between the Twitch group and the Kick group

    /// <summary>
    /// Draws "T ●  K ●" in the title bar: one letter + colored dot per active source.
    /// </summary>
    private void DrawConnectionDots(ID2D1DCRenderTarget target, float x, float maxWidth)
    {
        _connectionDotConnectedBrush ??= target.CreateSolidColorBrush(new Color4(0.23f, 0.65f, 0.36f, 1f)); // green
        _connectionDotConnectingBrush ??= target.CreateSolidColorBrush(new Color4(0.98f, 0.65f, 0.10f, 1f)); // amber
        _connectionDotErrorBrush ??= target.CreateSolidColorBrush(new Color4(0.93f, 0.27f, 0.27f, 1f)); // red

        float cursor = x;
        float rightEdge = x + maxWidth;
        float centerY = TitleBarHeight / 2f;

        cursor = DrawOneConnectionDot(target, "T", _twitchStatusKey, cursor, rightEdge, centerY);
        cursor += ConnectionDotGroupGap;
        DrawOneConnectionDot(target, "K", _kickStatusKey, cursor, rightEdge, centerY);
    }

    private float DrawOneConnectionDot(
        ID2D1DCRenderTarget target,
        string letter,
        string? statusKey,
        float x,
        float rightEdge,
        float centerY
    )
    {
        using var letterLayout = DWriteFactory.CreateTextLayout(
            letter,
            _titleBarLabelFormat!,
            rightEdge - x,
            TitleBarHeight
        );
        float letterWidth = (float)letterLayout.Metrics.WidthIncludingTrailingWhitespace;
        target.DrawTextLayout(new Vector2(x, 0f), letterLayout, _titleBarForegroundBrush);

        float dotCenterX = x + letterWidth + ConnectionDotGap + ConnectionDotRadius;
        var brush = ConnectionDotBrushFor(statusKey);
        if (brush is not null && dotCenterX + ConnectionDotRadius <= rightEdge)
        {
            target.FillEllipse(
                new Ellipse(new Vector2(dotCenterX, centerY), ConnectionDotRadius, ConnectionDotRadius),
                brush
            );
        }

        return dotCenterX + ConnectionDotRadius;
    }

    private ID2D1SolidColorBrush? ConnectionDotBrushFor(string? statusKey) =>
        statusKey switch
        {
            "MainWindow_ChannelConnected" => _connectionDotConnectedBrush,
            "MainWindow_Connecting" => _connectionDotConnectingBrush,
            "MainWindow_Disconnected" => _connectionDotErrorBrush,
            "MainWindow_ErrorLabel" => _connectionDotErrorBrush,
            _ => _connectionDotConnectingBrush,
        };

    private const float TitleBarHoverCornerRadius = 4f;

    private static void DrawTitleBarHoverBackground(
        ID2D1DCRenderTarget target,
        Rect rect,
        ID2D1SolidColorBrush brush,
        float progress,
        float maxOpacity
    )
    {
        if (progress <= 0f)
            return;

        brush.Opacity = progress * maxOpacity;
        var roundedRect = new RoundedRectangle
        {
            Rect = rect,
            RadiusX = TitleBarHoverCornerRadius,
            RadiusY = TitleBarHoverCornerRadius,
        };
        target.FillRoundedRectangle(roundedRect, brush);
        brush.Opacity = 1f;
    }

    private void DrawTitleBarSymbol(ID2D1DCRenderTarget target, Rect rect, string symbol)
    {

        float w = MathF.Max(1f, rect.Right - rect.Left);
        float h = MathF.Max(1f, rect.Bottom - rect.Top);

        using var layout = DWriteFactory.CreateTextLayout(symbol, _titleBarButtonFormat!, w, h);
        target.DrawTextLayout(new Vector2(rect.Left, rect.Top), layout, _titleBarForegroundBrush!);
    }

    private IDWriteTextFormat CreateTitleBarFormat(
        string fontFamily,
        FontWeight weight,
        float size,
        TextAlignment alignment
    )
    {
        var format = DWriteFactory.CreateTextFormat(
            fontFamily,
            weight,
            Vortice.DirectWrite.FontStyle.Normal,
            size
        );
        format.TextAlignment = alignment;
        format.ParagraphAlignment = ParagraphAlignment.Center;
        format.WordWrapping = WordWrapping.NoWrap;
        return format;
    }

    private static (Rect Settings, Rect Borders, Rect Close) GetTitleBarButtonRects(
        float width,
        float titleBarHeight
    )
    {

        var settings = new Rect(0f, 0f, SettingsButtonWidth, titleBarHeight);
        var close = new Rect(width - CloseButtonWidth, 0f, CloseButtonWidth, titleBarHeight);
        var borders = new Rect(
            width - CloseButtonWidth - BordersButtonWidth,
            0f,
            BordersButtonWidth,
            titleBarHeight
        );
        return (settings, borders, close);
    }

    private static bool Contains(Rect rect, int x, int y) =>
        x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;

    protected override bool IsInDraggableTitleBarArea(int clientX, int clientY)
    {
        if (_bordersHidden)
            return true;

        Win32.GetClientRect(Hwnd, out var client);
        float width = client.Right - client.Left;
        var (settingsRect, bordersRect, closeRect) = GetTitleBarButtonRects(width, TitleBarHeight);
        return !Contains(settingsRect, clientX, clientY)
            && !Contains(bordersRect, clientX, clientY)
            && !Contains(closeRect, clientX, clientY);
    }

    protected override void OnClientLButtonUp(int clientX, int clientY)
    {

        if (HandleModerationDropdownClick(clientX, clientY))
            return;

        if (!_bordersHidden && clientY <= TitleBarHeight)
        {
            Win32.GetClientRect(Hwnd, out var client);
            float width = client.Right - client.Left;
            var (settingsRect, bordersRect, closeRect) = GetTitleBarButtonRects(width, TitleBarHeight);

            if (Contains(settingsRect, clientX, clientY))
                OpenSettings();
            else if (Contains(bordersRect, clientX, clientY))
                ToggleBorders();
            else if (Contains(closeRect, clientX, clientY))
                ExitApplication();
            return;
        }

        if (_showingModeration)
        {
            HandleModerationRowClick(clientX, clientY);
            return;
        }

        if (!_showingEvents && _welcomeLoginButtonRect is { } welcomeLoginRect && Contains(welcomeLoginRect, clientX, clientY))
            HandleWelcomeLoginButtonClick();
    }

    protected override void OnClientMouseMove(int clientX, int clientY)
    {
        if (HandleModerationDropdownMouseMove(clientX, clientY))
            return;

        HandleModerationRowMouseMove(clientX, clientY);

        bool newWelcomeLoginHovered = !_showingModeration
            && !_showingEvents
            && _welcomeLoginButtonRect is { } welcomeLoginRect
            && Contains(welcomeLoginRect, clientX, clientY);
        if (newWelcomeLoginHovered != _welcomeLoginButtonHovered)
        {
            _welcomeLoginButtonHovered = newWelcomeLoginHovered;
            RequestRender();
        }

        var newHovered = TitleBarButton.None;
        if (!_bordersHidden && clientY <= TitleBarHeight)
        {
            Win32.GetClientRect(Hwnd, out var client);
            float width = client.Right - client.Left;
            var (settingsRect, bordersRect, closeRect) = GetTitleBarButtonRects(width, TitleBarHeight);

            if (Contains(settingsRect, clientX, clientY))
                newHovered = TitleBarButton.Settings;
            else if (Contains(bordersRect, clientX, clientY))
                newHovered = TitleBarButton.Borders;
            else if (Contains(closeRect, clientX, clientY))
                newHovered = TitleBarButton.Close;
        }

        if (newHovered == _hoveredButton)
            return;

        _hoveredButton = newHovered;
        EnsureHoverAnimationTimerRunning();
    }

    protected override void OnClientMouseLeave()
    {
        if (_welcomeLoginButtonHovered)
        {
            _welcomeLoginButtonHovered = false;
            RequestRender();
        }

        if (_moderationLoginButtonHovered)
        {
            _moderationLoginButtonHovered = false;
            RequestRender();
        }

        if (_hoveredChatSettingButton is not null)
        {
            _hoveredChatSettingButton = null;
            EnsureHoverAnimationTimerRunning();
        }

        if (_hoveredButton == TitleBarButton.None)
            return;

        _hoveredButton = TitleBarButton.None;
        EnsureHoverAnimationTimerRunning();
    }

    private void EnsureHoverAnimationTimerRunning()
    {
        _lastHoverTickUtc = DateTime.UtcNow;
        _hoverAnimationTimer ??= new System.Threading.Timer(
            _ => PostToUiThread(AdvanceHoverAnimation),
            null,
            16,
            16
        );
    }

    private void AdvanceHoverAnimation()
    {
        var now = DateTime.UtcNow;
        float dt = (float)(now - _lastHoverTickUtc).TotalSeconds;
        _lastHoverTickUtc = now;

        const float speed = 18f;
        float t = 1f - MathF.Exp(-speed * dt);

        bool moving = StepTowards(ref _settingsHoverProgress, _hoveredButton == TitleBarButton.Settings ? 1f : 0f, t);
        moving |= StepTowards(ref _bordersHoverProgress, _hoveredButton == TitleBarButton.Borders ? 1f : 0f, t);
        moving |= StepTowards(ref _closeHoverProgress, _hoveredButton == TitleBarButton.Close ? 1f : 0f, t);
        moving |= StepTowards(
            ref _followerButtonHoverProgress,
            _hoveredChatSettingButton == ModerationChatSettingField.Follower ? 1f : 0f,
            t
        );
        moving |= StepTowards(
            ref _slowButtonHoverProgress,
            _hoveredChatSettingButton == ModerationChatSettingField.Slow ? 1f : 0f,
            t
        );

        RequestRender();

        if (!moving)
        {
            _hoverAnimationTimer?.Dispose();
            _hoverAnimationTimer = null;
        }
    }

    private static bool StepTowards(ref float current, float target, float t)
    {
        const float epsilon = 0.002f;
        if (MathF.Abs(current - target) < epsilon)
        {
            if (current != target)
                current = target;
            return false;
        }
        current += (target - current) * t;
        return true;
    }
}