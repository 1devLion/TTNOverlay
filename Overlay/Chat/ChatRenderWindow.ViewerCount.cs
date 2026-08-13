using TTNOverlay.Services;
using TTNOverlay.Twitch;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the viewer count badge shown in the title bar, including periodic refresh.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private const float ViewerCountBaseFontSize = 13f;
    private const float ViewerCountBasePaddingX = 6f;
    private const float ViewerCountBasePaddingY = 3f;
    private const float ViewerCountBaseCornerRadius = 4f;
    private const float ViewerCountBadgeMarginTop = 4f;
    private const float ViewerCountBadgeMarginRight = 4f;

    private System.Threading.Timer? _viewerCountTimer;

    private int? _viewerCount;

    private ID2D1SolidColorBrush? _viewerCountBadgeBrush;
    private ID2D1SolidColorBrush? _viewerCountTextBrush;
    private IDWriteTextFormat? _viewerCountFormat;

    private void SetupViewerCountWidget()
    {
        _viewerCountTimer?.Dispose();
        _viewerCountTimer = null;
        _viewerCount = null;

        bool hasCredentials = _settings.EnableTwitchApi;
        bool needsHelix = hasCredentials && (_settings.ShowViewerCount || _settings.ShowBadges);

        if (needsHelix)
        {
            _helix ??= new HelixClient(TwitchAuthService.ClientId);
            _moderation ??= new ModerationService(_settings);
        }
        else
        {
            _helix = null;
        }

        if (_helix is null || !_settings.ShowViewerCount)
        {
            RequestRender();
            return;
        }

        _viewerCountTimer = new System.Threading.Timer(
            _ => PostToUiThread(() => _ = RefreshViewerCountAsync()),
            null,
            0,
            60_000
        );
    }

    private async Task RefreshViewerCountAsync()
    {
        if (_helix is null || string.IsNullOrWhiteSpace(_settings.Channel))
            return;

        var token = _moderation is null ? null : await _moderation.GetAccessTokenAsync();
        if (token is null)
            return;

        var count = await _helix.GetViewerCountAsync(_settings.Channel, token);

        PostToUiThread(() =>
        {
            _viewerCount = count;
            RequestRender();
        });
    }

    private void DrawViewerCountBadge(ID2D1DCRenderTarget target, float width)
    {
        if (_viewerCount is not { } count)
            return;

        _viewerCountBadgeBrush ??= target.CreateSolidColorBrush(GetViewerCountBackgroundColor());

        _viewerCountTextBrush ??= target.CreateSolidColorBrush(ThemeService.OverlayText);

        float fontSize = (float)_settings.ViewerCountSize;
        float scale = fontSize / ViewerCountBaseFontSize;

        _viewerCountFormat ??= CreateTitleBarFormat(
            "Segoe UI",
            FontWeight.Normal,
            fontSize,
            TextAlignment.Leading
        );
        _viewerCountFormat.ParagraphAlignment = ParagraphAlignment.Near;

        string text = $"\U0001F441 {count:N0}";
        using var layout = DWriteFactory.CreateTextLayout(text, _viewerCountFormat, 200f, 100f);
        var metrics = layout.Metrics;

        float paddingX = ViewerCountBasePaddingX * scale;
        float paddingY = ViewerCountBasePaddingY * scale;

        float boxWidth = metrics.Width + paddingX * 2f;
        float boxHeight = metrics.Height + paddingY * 2f;
        float boxX = width - ViewerCountBadgeMarginRight - boxWidth;
        float boxY = TitleBarHeight + ViewerCountBadgeMarginTop;

        var badgeRect = new RoundedRectangle
        {
            Rect = new Rect(boxX, boxY, boxWidth, boxHeight),
            RadiusX = ViewerCountBaseCornerRadius * scale,
            RadiusY = ViewerCountBaseCornerRadius * scale,
        };
        target.FillRoundedRectangle(badgeRect, _viewerCountBadgeBrush);
        target.DrawTextLayout(
            new System.Numerics.Vector2(boxX + paddingX, boxY + paddingY),
            layout,
            _viewerCountTextBrush
        );
    }

    /// <summary>
    /// Resolves the badge background color: a user-picked color if set (Settings.ViewerCountBackgroundColor),
    /// otherwise the theme-based default that was previously hardcoded here.
    /// </summary>
    private Color4 GetViewerCountBackgroundColor()
    {
        byte alpha = _settings.ViewerCountBackgroundAlpha;
        if (!string.IsNullOrWhiteSpace(_settings.ViewerCountBackgroundColor) &&
            ColorPickerWindow.TryParseHex(_settings.ViewerCountBackgroundColor, out var r, out var g, out var b))
        {
            return new Color4(r / 255f, g / 255f, b / 255f, alpha / 255f);
        }

        return ThemeService.IsDark
            ? new Color4(0f, 0f, 0f, alpha / 255f)
            : new Color4(0xF2 / 255f, 0xF2 / 255f, 0xF2 / 255f, alpha / 255f);
    }

    private void DisconnectViewerCount()
    {
        _viewerCountTimer?.Dispose();
        _viewerCountTimer = null;
    }
}