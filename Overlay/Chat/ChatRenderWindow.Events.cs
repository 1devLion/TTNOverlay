using System.Numerics;
using TTNOverlay.Models;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: rendering and interaction for individual event entries in the chat/dashboard view.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private const float EventBannerPaddingX = 8f;
    private const float EventBannerPaddingY = 4f;
    private const float EventBannerCornerRadius = 6f;

    private AppSettings _settings = SettingsService.Load();

    private float BadgeSize => (float)Math.Ceiling(_settings.FontSize);
    private float EmoteSize => (float)Math.Ceiling(_settings.FontSize * 1.4);

    private ID2D1SolidColorBrush? _eventTextBrush;
    private IDWriteTextFormat? _eventIconFormat;

    private IDWriteTextFormat? _eventNameFormat;
    private IDWriteTextFormat? _eventBodyFormat;

    private float DrawEventBanner(ID2D1DCRenderTarget target, ChatMessage msg, float x, float y, float maxWidth, float clipHeight, bool draw = true)
    {
        var (icon, defaultBg) = GetEventStyle(msg.EventKind, msg.Platform, msg.AnnouncementColor);

        float iconSize = (float)Math.Ceiling(_settings.FontSize * 1.8);
        string? iconUrl = !string.IsNullOrEmpty(msg.EventImageUrl) ? msg.EventImageUrl : GetIrcEventGifPath(msg);
        bool hasIcon = !string.IsNullOrEmpty(iconUrl) || !string.IsNullOrEmpty(icon);
        float iconColumnWidth = hasIcon ? iconSize + 6f : 0f;

        float innerX = x + EventBannerPaddingX + iconColumnWidth;
        float innerMaxWidth = Math.Max(maxWidth - EventBannerPaddingX * 2 - iconColumnWidth, 20f);

        float measuredBottom = DrawEventBannerText(target, msg, innerX, y + EventBannerPaddingY, innerMaxWidth, clipHeight, draw: false);
        float textHeight = measuredBottom - (y + EventBannerPaddingY);

        float contentHeight = Math.Max(textHeight, hasIcon ? iconSize : 0f) + EventBannerPaddingY * 2;

        if (draw)
        {
            _eventTextBrush ??= target.CreateSolidColorBrush(ThemeService.PureContrastTint);

            var bg = ResolveEventBoxColor(msg.EventType, defaultBg);
            using var bgBrush = target.CreateSolidColorBrush(new Color4(bg.R, bg.G, bg.B, 0xDD / 255f));

            float iconY = y + (contentHeight - iconSize) / 2f;

            var roundedRect = new RoundedRectangle
            {
                Rect = new Rect(x, y, maxWidth, contentHeight),
                RadiusX = EventBannerCornerRadius,
                RadiusY = EventBannerCornerRadius,
            };
            target.FillRoundedRectangle(roundedRect, bgBrush);

            if (!string.IsNullOrEmpty(iconUrl))
            {
                DrawEventIcon(target, iconUrl!, x + EventBannerPaddingX, iconY, iconSize);
            }
            else if (!string.IsNullOrEmpty(icon))
            {
                _eventIconFormat ??= DWriteFactory.CreateTextFormat(
                    "Segoe UI Emoji", FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, (float)_settings.FontSize
                );
                using var iconLayout = DWriteFactory.CreateTextLayout(icon, _eventIconFormat, iconSize, iconSize);
                target.DrawTextLayout(new Vector2(x + EventBannerPaddingX, iconY), iconLayout, _eventTextBrush!);
            }

            DrawEventBannerText(target, msg, innerX, y + EventBannerPaddingY, innerMaxWidth, clipHeight, draw: true);
        }

        return y + contentHeight;
    }

    private float DrawEventBannerText(ID2D1DCRenderTarget target, ChatMessage msg, float x, float y, float maxWidth, float clipHeight, bool draw)
    {
        _eventNameFormat ??= DWriteFactory.CreateTextFormat(
            "Segoe UI", FontWeight.Bold, Vortice.DirectWrite.FontStyle.Normal, (float)(_settings.FontSize * 0.9)
        );
        _eventBodyFormat ??= DWriteFactory.CreateTextFormat(
            "Segoe UI", FontWeight.SemiBold, Vortice.DirectWrite.FontStyle.Normal, (float)(_settings.FontSize * 0.9)
        );

        float cursorY = y;

        if (!string.IsNullOrEmpty(msg.DisplayName))
        {
            using var nameLayout = DWriteFactory.CreateTextLayout(msg.DisplayName + ": ", _eventNameFormat!, maxWidth, UnboundedLayoutHeight);
            if (draw)
                DrawTextWithOutline(target, new Vector2(x, cursorY), nameLayout, _eventTextBrush!);
            cursorY += (float)nameLayout.Metrics.Height;
        }

        if (!string.IsNullOrEmpty(msg.Text) && cursorY < clipHeight)
            cursorY = DrawBody(target, msg, x, cursorY, maxWidth, clipHeight, _eventTextBrush!, out _, draw, format: _eventBodyFormat);

        return cursorY;
    }

    private void DrawEventIcon(ID2D1DCRenderTarget target, string url, float x, float y, float size)
    {
        string key = $"eventicon:{url}";
        TryLoadAnimatedImage(key, animatedCacheKey: url, url, size);

        ID2D1Bitmap? bitmap;
        if (_animatedImageCache.TryGetValue(key, out var frames))
        {
            if (frames is not null)
            {
                bitmap = frames[_animationState.TryGetValue(key, out var st) ? st.Index : 0].Bitmap;
            }
            else
            {
                bitmap = GetOrLoadImageBitmap(key, url, DecodeTargetSize((int)size));
            }
        }
        else
        {
            bitmap = null;
        }

        if (bitmap is not null)
            DrawBitmapAt(target, bitmap, x, y, size);
    }

    // Switches on the canonical (EventType, Platform) pair, not the raw event-id string.
    // Most kinds render identically regardless of platform (Raid, Announcement, Bits, ...) and use the
    // "_" wildcard for Platform. A few gift-sub kinds keep DIFFERENT colors per platform on purpose. 
    // That divergence already existed before this refactor (Streamlabs' gift-sub variants were always a
    // flat purple, distinct from Twitch's own per-type colors), so it's preserved explicitly here
    // instead of being flattened away. EventType.Unknown (any event this app doesn't specifically
    // classify yet, on any current or future platform) keeps the generic "info" style, same fallback as
    // before.
    private static (string Icon, Color4 BgColor) GetEventStyle(EventType eventKind, Platform? platform, string? announcementColor) =>
        (eventKind, platform) switch
        {
            (EventType.Sub, _) => ("🎉", new Color4(0x9B / 255f, 0x4D / 255f, 0xCA / 255f, 1f)),
            (EventType.Resub, _) => ("🎉", new Color4(0x6D / 255f, 0x28 / 255f, 0xD9 / 255f, 1f)),

            (EventType.SubGift, Platform.Streamlabs) => ("🎁", new Color4(0x9B / 255f, 0x4D / 255f, 0xCA / 255f, 1f)),
            (EventType.SubGift, _) => ("🎁", new Color4(0xC0 / 255f, 0x26 / 255f, 0xD3 / 255f, 1f)),

            (EventType.AnonSubGift, Platform.Streamlabs) => ("🎭", new Color4(0x9B / 255f, 0x4D / 255f, 0xCA / 255f, 1f)),
            (EventType.AnonSubGift, _) => ("🎭", new Color4(0x86 / 255f, 0x19 / 255f, 0x8F / 255f, 1f)),

            (EventType.MysteryGiftSub, Platform.Streamlabs) => ("✨", new Color4(0x9B / 255f, 0x4D / 255f, 0xCA / 255f, 1f)),
            (EventType.MysteryGiftSub, _) => ("✨", new Color4(0xDB / 255f, 0x27 / 255f, 0x77 / 255f, 1f)),

            (EventType.AnonMysteryGiftSub, Platform.Streamlabs) => ("🎭", new Color4(0x9B / 255f, 0x4D / 255f, 0xCA / 255f, 1f)),
            (EventType.AnonMysteryGiftSub, _) => ("🎭", new Color4(0x9D / 255f, 0x17 / 255f, 0x4D / 255f, 1f)),

            (EventType.PrimeUpgrade, _) => ("⭐", new Color4(0x4F / 255f, 0x46 / 255f, 0xE5 / 255f, 1f)),
            (EventType.GiftUpgrade, _) => ("⭐", new Color4(0x7C / 255f, 0x3A / 255f, 0xED / 255f, 1f)),
            (EventType.AnonGiftUpgrade, _) => ("⭐", new Color4(0x5B / 255f, 0x21 / 255f, 0xB6 / 255f, 1f)),
            (EventType.Raid, _) => ("⚔️", new Color4(0xFF / 255f, 0x7A / 255f, 0x00 / 255f, 1f)),
            (EventType.Ritual, _) => ("👋", new Color4(0x00 / 255f, 0x9E / 255f, 0x9E / 255f, 1f)),
            (EventType.BitsBadgeTier, _) => ("💎", new Color4(0x00 / 255f, 0x90 / 255f, 0xFF / 255f, 1f)),
            (EventType.Announcement, _) => ("📢", AnnouncementColor(announcementColor)),

            (EventType.Donation, _) => ("💰", new Color4(0x1F / 255f, 0xA0 / 255f, 0x5C / 255f, 1f)),
            (EventType.Follow, _) => ("💜", new Color4(0x9B / 255f, 0x4D / 255f, 0xCA / 255f, 1f)),
            (EventType.Host, _) => ("📡", new Color4(0x00 / 255f, 0x82 / 255f, 0xFF / 255f, 1f)),
            (EventType.Merch, _) => ("🛍️", new Color4(0xFF / 255f, 0x7A / 255f, 0x00 / 255f, 1f)),
            (EventType.Bits, _) => ("💎", new Color4(0x00 / 255f, 0x90 / 255f, 0xFF / 255f, 1f)),
            (EventType.PowerUp, _) => ("⚡", new Color4(0x00 / 255f, 0x90 / 255f, 0xFF / 255f, 1f)),

            _ => ("ℹ️", new Color4(0x60 / 255f, 0x60 / 255f, 0x60 / 255f, 1f)),
        };

    private static Color4 AnnouncementColor(string? name) =>
        name switch
        {
            "blue" => new Color4(0x00 / 255f, 0x82 / 255f, 0xFF / 255f, 1f),
            "green" => new Color4(0x00 / 255f, 0xB2 / 255f, 0x4F / 255f, 1f),
            "orange" => new Color4(0xFF / 255f, 0x7A / 255f, 0x00 / 255f, 1f),
            "purple" => new Color4(0x9B / 255f, 0x4D / 255f, 0xCA / 255f, 1f),
            _ => new Color4(0x1F / 255f, 0x69 / 255f, 0xFF / 255f, 1f),
        };

    private Color4 ResolveEventBoxColor(string? eventType, Color4 defaultColor)
    {
        Color4 themeColor = ThemeService.IsDark
            ? new Color4(0f, 0f, 0f, 1f)
            : new Color4(0xE8 / 255f, 0xE8 / 255f, 0xE8 / 255f, 1f);

        if (!_settings.EventBoxColorAdvancedMode || eventType is null
            || !_settings.EventBoxColorModes.TryGetValue(eventType, out var mode))
            return themeColor;

        return mode switch
        {
            "Original" => defaultColor,
            "Custom" when _settings.EventBoxColors.TryGetValue(eventType, out var hex) && !string.IsNullOrEmpty(hex)
                => ParseHexColor(hex),
            _ => themeColor,
        };
    }

    private string? GetIrcEventGifPath(ChatMessage msg)
    {
        if (!_settings.EnableIrcEventGif)
            return null;

        if (!string.IsNullOrEmpty(msg.EventImageUrl))
            return null;

        if (!_settings.IrcEventGifAdvancedMode)
            return _settings.IrcEventGifPath ?? "";

        var eventType = msg.EventType;
        if (string.IsNullOrEmpty(eventType))
            return _settings.IrcEventGifPath ?? "";

        if (_settings.IrcEventGifPaths.TryGetValue(eventType, out var customPath) && !string.IsNullOrEmpty(customPath))
            return customPath;

        return _settings.IrcEventGifPath ?? "";
    }
}