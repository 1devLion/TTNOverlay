using System.Linq;
using TTNOverlay.Services;
using TTNOverlay.Twitch;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the viewer count badge shown in the title bar, including periodic
/// refresh. Sources the count from whichever platforms are connected and toggled on in
/// Settings.ViewerCountInclude* (Twitch via Helix, Kick via the already-connected IKickChatClient;
/// YouTube isn't wired up yet). Fetching one platform never depends on another being enabled --
/// Twitch still needs EnableTwitchApi + a moderator login (Helix requires an access token), but Kick
/// needs neither, so a Kick-only viewer count works even with the Twitch API section untouched.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private const float ViewerCountBaseFontSize = 13f;
    private const float ViewerCountBasePaddingX = 6f;
    private const float ViewerCountBasePaddingY = 3f;
    private const float ViewerCountBaseCornerRadius = 4f;
    private const float ViewerCountBadgeMarginTop = 4f;
    private const float ViewerCountBadgeMarginRight = 4f;
    private const float ViewerCountIconGap = 4f;

    private System.Threading.Timer? _viewerCountTimer;

    private int? _twitchViewerCount;
    private int? _kickViewerCount;

    private ID2D1SolidColorBrush? _viewerCountBadgeBrush;
    private ID2D1SolidColorBrush? _viewerCountTextBrush;
    private IDWriteTextFormat? _viewerCountFormat;

    private void SetupViewerCountWidget()
    {
        _viewerCountTimer?.Dispose();
        _viewerCountTimer = null;
        _twitchViewerCount = null;
        _kickViewerCount = null;

        // Helix (Twitch) is the only source that needs credentials: badges also ride on it, so keep
        // creating/tearing it down based on EnableTwitchApi regardless of the viewer count toggles.
        bool needsHelix = _settings.EnableTwitchApi && (_settings.ShowViewerCount || _settings.ShowBadges);
        if (needsHelix)
        {
            _helix ??= new HelixClient(TwitchAuthService.ClientId);
            _moderation ??= new ModerationService(_settings);
        }
        else
        {
            _helix = null;
        }

        // The timer itself must not depend on Twitch: a Kick-only viewer count (ViewerCountIncludeKick
        // with EnableTwitchApi off) still needs to poll.
        if (!_settings.ShowViewerCount)
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
        int? twitchCount = null;
        int? kickCount = null;

        if (_settings.ViewerCountIncludeTwitch && _helix is not null && !string.IsNullOrWhiteSpace(_settings.Channel))
        {
            var token = _moderation is null ? null : await _moderation.GetAccessTokenAsync();
            if (token is not null)
                twitchCount = await _helix.GetViewerCountAsync(_settings.Channel, token);
        }

        if (_settings.ViewerCountIncludeKick && _kickActive)
            kickCount = await _kick.GetViewerCountAsync();

        // ViewerCountIncludeYouTube: no YouTube client yet, intentionally left out until that
        // integration lands.

        PostToUiThread(() =>
        {
            _twitchViewerCount = twitchCount;
            _kickViewerCount = kickCount;
            RequestRender();
        });
    }

    /// <summary>
    /// Builds the badge's display text plus, when the count boils down to a single platform (either
    /// because only one is toggled on, or only one actually returned a value), the local icon key for
    /// that platform's logo -- e.g. "platform/twitch" -- so the badge shows the logo instead of the
    /// generic eye glyph.
    /// </summary>
    private (string Text, string? PlatformIconKey) BuildViewerCountContent()
    {
        var parts = new List<(string Label, int Count, string IconKey)>();
        if (_settings.ViewerCountIncludeTwitch && _twitchViewerCount is { } tc)
            parts.Add(("Twitch", tc, "platform/twitch"));
        if (_settings.ViewerCountIncludeKick && _kickViewerCount is { } kc)
            parts.Add(("Kick", kc, "platform/kick"));

        if (parts.Count == 0)
            return ("", null);

        if (parts.Count == 1)
            return ($"{parts[0].Count:N0}", parts[0].IconKey);

        if (_settings.ViewerCountDisplayMode == "PerPlatform")
            return (string.Join("   ", parts.Select(p => $"{p.Label} {p.Count:N0}")), null);

        int total = parts.Sum(p => p.Count);
        return ($"\U0001F441 {total:N0}", null);
    }

    private void DrawViewerCountBadge(ID2D1DCRenderTarget target, float width)
    {
        var (text, platformIconKey) = BuildViewerCountContent();
        if (text.Length == 0)
            return;

        _viewerCountBadgeBrush ??= target.CreateSolidColorBrush(GetViewerCountBackgroundColor());

        _viewerCountTextBrush ??= target.CreateSolidColorBrush(GetViewerCountTextColor());

        float fontSize = (float)_settings.ViewerCountSize;
        float scale = fontSize / ViewerCountBaseFontSize;

        _viewerCountFormat ??= CreateTitleBarFormat(
            "Segoe UI",
            FontWeight.Normal,
            fontSize,
            TextAlignment.Leading
        );
        _viewerCountFormat.ParagraphAlignment = ParagraphAlignment.Near;

        ID2D1Bitmap? iconBitmap = platformIconKey is null
            ? null
            : GetOrCreateLocalBadgeBitmap(target, platformIconKey, platformIconKey);
        float iconSize = iconBitmap is null ? 0f : (float)_settings.ViewerCountSize;
        float iconAdvance = iconBitmap is null ? 0f : iconSize + ViewerCountIconGap * scale;

        using var layout = DWriteFactory.CreateTextLayout(text, _viewerCountFormat, 200f, 100f);
        var metrics = layout.Metrics;

        float paddingX = ViewerCountBasePaddingX * scale;
        float paddingY = ViewerCountBasePaddingY * scale;

        float boxWidth = iconAdvance + metrics.Width + paddingX * 2f;
        float boxHeight = System.Math.Max(metrics.Height, iconSize) + paddingY * 2f;
        float boxX = width - ViewerCountBadgeMarginRight - boxWidth;
        float boxY = TitleBarHeight + ViewerCountBadgeMarginTop;

        var badgeRect = new RoundedRectangle
        {
            Rect = new Rect(boxX, boxY, boxWidth, boxHeight),
            RadiusX = ViewerCountBaseCornerRadius * scale,
            RadiusY = ViewerCountBaseCornerRadius * scale,
        };
        target.FillRoundedRectangle(badgeRect, _viewerCountBadgeBrush);

        if (iconBitmap is not null)
            DrawBitmapAt(target, iconBitmap, boxX + paddingX, boxY + (boxHeight - iconSize) / 2f, iconSize);

        target.DrawTextLayout(
            new System.Numerics.Vector2(boxX + paddingX + iconAdvance, boxY + paddingY),
            layout,
            _viewerCountTextBrush
        );
    }

    /// <summary>
    /// Resolves the badge background color: a user-picked color if set (Settings.ViewerCountBackgroundColor),
    /// otherwise the theme-based default.
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

    private Color4 GetViewerCountTextColor()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ViewerCountTextColor) &&
            ColorPickerWindow.TryParseHex(_settings.ViewerCountTextColor, out var r, out var g, out var b))
        {
            return new Color4(r / 255f, g / 255f, b / 255f, 1f);
        }

        return ThemeService.OverlayText;
    }

    private void DisconnectViewerCount()
    {
        _viewerCountTimer?.Dispose();
        _viewerCountTimer = null;
    }
}