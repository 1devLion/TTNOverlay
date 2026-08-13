using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TTNOverlay.Models;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the main Direct2D drawing routine for the chat/events view.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private sealed class MessageHeightCacheEntry
    {
        public float Width;
        public double FontSize;
        public string Channel = "";
        public float Height;
    }

    private readonly ConditionalWeakTable<ChatMessage, MessageHeightCacheEntry> _messageHeightCache = new();

    private void InvalidateMessageHeightCache() => _messageHeightCache.Clear();

    /// <summary>
    /// Caches the actual IDWriteTextLayout used to draw a non-emote message's body (Paso 2 / draw
    /// pass of OnRender), keyed the same way as _messageHeightCache (per ChatMessage, tied to a
    /// ConditionalWeakTable so entries die with the message once it scrolls out and MaxMessages
    /// trims it -- no manual eviction needed). Without this, DrawBody recreated a fresh layout via
    /// DWriteFactory.CreateTextLayout on every single frame for every visible non-emote message,
    /// even though the text/width/format hadn't changed since the previous frame -- the same
    /// per-frame-recreation problem _wordLayoutCache already solves for the emote path.
    /// Invalidated (Dispose + Clear) anywhere _bodyFormat itself gets disposed, since a cached
    /// layout is only valid for the exact IDWriteTextFormat instance it was created with.
    /// </summary>
    private sealed class BodyLayoutCacheEntry
    {
        public IDWriteTextLayout Layout = null!;
        public float Width;
        public IDWriteTextFormat Format = null!;
    }

    private readonly ConditionalWeakTable<ChatMessage, BodyLayoutCacheEntry> _bodyLayoutCache = new();

    private void InvalidateBodyLayoutCache()
    {
        foreach (var (_, entry) in _bodyLayoutCache)
            entry.Layout.Dispose();
        _bodyLayoutCache.Clear();
    }

    private IDWriteTextLayout GetOrCreateBodyLayout(ChatMessage msg, IDWriteTextFormat format, float maxWidth)
    {
        if (_bodyLayoutCache.TryGetValue(msg, out var cached)
            && cached.Width == maxWidth
            && ReferenceEquals(cached.Format, format))
        {
            return cached.Layout;
        }

        var layout = DWriteFactory.CreateTextLayout(msg.Text, format, maxWidth, UnboundedLayoutHeight);

        if (cached is not null)
        {
            cached.Layout.Dispose();
            cached.Layout = layout;
            cached.Width = maxWidth;
            cached.Format = format;
        }
        else
        {
            _bodyLayoutCache.Add(msg, new BodyLayoutCacheEntry { Layout = layout, Width = maxWidth, Format = format });
        }

        return layout;
    }

    private float GetOrMeasureHeight(ID2D1DCRenderTarget target, ChatMessage msg, bool isEvent, float x, float maxWidth)
    {
        string channel = _settings.Channel ?? "";
        if (_messageHeightCache.TryGetValue(msg, out var cached)
            && cached.Width == maxWidth
            && cached.FontSize == _settings.FontSize
            && cached.Channel == channel)
        {
            return cached.Height;
        }

        float height = isEvent
            ? DrawEventBanner(target, msg, x, 0f, maxWidth, float.MaxValue, draw: false)
            : DrawMessage(target, msg, x, 0f, maxWidth, float.MaxValue, draw: false);

        _messageHeightCache.AddOrUpdate(msg, new MessageHeightCacheEntry
        {
            Width = maxWidth,
            FontSize = _settings.FontSize,
            Channel = channel,
            Height = height,
        });
        return height;
    }

    protected override void OnRender(ID2D1DCRenderTarget target)
    {
        _target ??= target;

        if (_lastKnownIsDark != ThemeService.IsDark)
        {
            bool firstFrame = _lastKnownIsDark is null;
            _lastKnownIsDark = ThemeService.IsDark;
            if (!firstFrame)
                InvalidateSettingsDependentResources();
        }

        _titleBarBrush ??= target.CreateSolidColorBrush(
            ThemeService.IsDark ? new Color4(0f, 0f, 0f, 0.55f) : new Color4(1f, 1f, 1f, 0.55f)
        );
        _moderationBackgroundBrush ??= target.CreateSolidColorBrush(ThemeService.WindowBackground);
        _bodyBrush ??= target.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
        _systemBrush ??= target.CreateSolidColorBrush(ParseHexColor(ChatColors.SystemGray));
        _resizeGripBrush ??= target.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));

        _usernameFormat ??= DWriteFactory.CreateTextFormat(
            "Segoe UI",
            FontWeight.Bold,
            Vortice.DirectWrite.FontStyle.Normal,
            (float)_settings.FontSize
        );
        _bodyFormat ??= DWriteFactory.CreateTextFormat(
            "Segoe UI",
            FontWeight.Normal,
            Vortice.DirectWrite.FontStyle.Normal,
            (float)_settings.FontSize
        );
        _systemFormat ??= DWriteFactory.CreateTextFormat(
            "Segoe UI",
            FontWeight.Normal,
            Vortice.DirectWrite.FontStyle.Italic,
            (float)(_settings.FontSize * 0.85)
        );

        Win32.GetClientRect(Hwnd, out var client);
        float width = client.Right - client.Left;
        float height = client.Bottom - client.Top;

        if (width <= 0 || height <= 0)
            return;

        _hitTestCatcherBrush ??= target.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 1f / 255f));
        target.FillRectangle(new Rect(0f, 0f, width, height), _hitTestCatcherBrush);

        if (_showingModeration)
            target.FillRectangle(new Rect(0f, 0f, width, height), _moderationBackgroundBrush);

        if (!_bordersHidden)
        {
            target.FillRectangle(new Rect(0f, 0f, width, TitleBarHeight), _titleBarBrush);
            DrawTitleBar(target, width);
        }
        DrawViewerCountBadge(target, width);

        float maxTextWidth = width - Padding * 2;
        if (maxTextWidth <= 0)
            return;

        float y = TitleBarHeight + Padding;
        float visibleHeight = Math.Max(0f, height - y);

        if (_showingModeration)
        {
            DrawModerationPanel(target, width, height, y, visibleHeight);
            DrawModerationDropdown(target);
        }
        else
        {
            _welcomeLoginButtonRect = null;

            var activeList = _showingEvents ? _dashboardEvents : _messages;

            float measureY = 0f;
            float addedAtBottomHeight = 0f;
            var lastNewestRef = _showingEvents ? _eventsLastNewestMsg : _messagesLastNewestMsg;
            bool pastLastNewest = lastNewestRef is null;

            var heights = new List<float>(activeList.Count);
            foreach (var msg in activeList)
            {
                float msgHeight = GetOrMeasureHeight(target, msg, _showingEvents, Padding, maxTextWidth);
                heights.Add(msgHeight);
                measureY += msgHeight + MessageSpacing;

                if (pastLastNewest)
                    addedAtBottomHeight += msgHeight + MessageSpacing;
                else if (ReferenceEquals(msg, lastNewestRef))
                    pastLastNewest = true;
            }
            float totalContentHeight = activeList.Count > 0 ? measureY - MessageSpacing : 0f;
            float overflow = Math.Max(0f, totalContentHeight - visibleHeight);
            var newestMsg = activeList.Count > 0 ? activeList[^1] : null;

            if (_showingEvents)
            {
                if (_eventsScrollOffset > 0f)
                    _eventsScrollOffset += addedAtBottomHeight;
                _eventsLastNewestMsg = newestMsg;

                _eventsScrollOverflow = overflow;
                _eventsScrollOffset = Math.Clamp(_eventsScrollOffset, 0f, overflow);
            }
            else
            {
                if (_messagesScrollOffset > 0f)
                    _messagesScrollOffset += addedAtBottomHeight;
                _messagesLastNewestMsg = newestMsg;

                _messagesScrollOverflow = overflow;
                _messagesScrollOffset = Math.Clamp(_messagesScrollOffset, 0f, overflow);
            }
            float scrollOffset = _showingEvents ? _eventsScrollOffset : _messagesScrollOffset;

            float startY = y - overflow + scrollOffset;

            target.PushAxisAlignedClip(new Rect(0f, y, width, visibleHeight), AntialiasMode.PerPrimitive);
            try
            {
                float cursorY = startY;
                for (int i = 0; i < activeList.Count; i++)
                {
                    if (cursorY >= height)
                        break;

                    var msg = activeList[i];
                    float msgHeight = heights[i];

                    if (cursorY + msgHeight < y)
                    {
                        cursorY += msgHeight + MessageSpacing;
                        continue;
                    }

                    cursorY = _showingEvents
                        ? DrawEventBanner(target, msg, Padding, cursorY, maxTextWidth, height, draw: true)
                        : DrawMessage(target, msg, Padding, cursorY, maxTextWidth, height, draw: true);
                    cursorY += MessageSpacing;
                }
            }
            finally
            {
                target.PopAxisAlignedClip();
            }
        }

        if (!_clickThroughEnabled && !_bordersHidden)
        {
            target.FillRectangle(
                new Rect(width - ResizeGripSize, height - ResizeGripSize, ResizeGripSize, ResizeGripSize),
                _resizeGripBrush
            );
        }

        float flashOpacity = CurrentFlashOpacity();
        if (flashOpacity > 0f)
        {
            var baseColor = _testFlashColor ?? ParseFlashColor(_settings.AlertFlashColor, _settings.AlertFlashAlpha);
            _flashBrush ??= target.CreateSolidColorBrush(baseColor);
            _flashBrush.Color = new Color4(baseColor.R, baseColor.G, baseColor.B, baseColor.A * flashOpacity);
            target.FillRectangle(new Rect(0f, 0f, width, height), _flashBrush);
        }
    }

    private const float UnboundedLayoutHeight = float.MaxValue;

    private static readonly Vector2[] OutlineOffsets =
    {
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1, 0), new(1, 0),
        new(-1, 1), new(0, 1), new(1, 1),
    };

    private void DrawTextWithOutline(
        ID2D1DCRenderTarget target,
        Vector2 origin,
        IDWriteTextLayout layout,
        ID2D1Brush fillBrush
    )
    {
        _outlineBrush ??= target.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0.20f));

        foreach (var offset in OutlineOffsets)
            target.DrawTextLayout(origin + offset, layout, _outlineBrush);

        target.DrawTextLayout(origin, layout, fillBrush);
    }

    private float DrawMessage(
        ID2D1DCRenderTarget target,
        ChatMessage msg,
        float x,
        float y,
        float maxWidth,
        float clipHeight,
        bool draw = true
    )
    {
        if (msg.IsSystem)
        {
            using var systemLayout = DWriteFactory.CreateTextLayout(
                msg.Text,
                _systemFormat!,
                maxWidth,
                UnboundedLayoutHeight
            );
            if (draw)
                DrawTextWithOutline(target, new Vector2(x, y), systemLayout, _systemBrush!);

            float bottom = y + (float)systemLayout.Metrics.Height;

            if (msg.IsTwitchLoginPrompt && !(_moderation?.IsLoggedIn ?? false))
                bottom = DrawWelcomeTwitchLoginButton(target, x, bottom, draw);

            return bottom;
        }

        if (msg.EventType is not null)
            return DrawEventBanner(target, msg, x, y, maxWidth, clipHeight, draw);

        return DrawNormalMessage(target, msg, x, y, maxWidth, clipHeight, draw);
    }

    private float DrawNormalMessage(
        ID2D1DCRenderTarget target,
        ChatMessage msg,
        float x,
        float y,
        float maxWidth,
        float clipHeight,
        bool draw = true
    )
    {
        bool isMention = IsMentionOfChannel(msg.Text);
        if (!isMention)
            return DrawNormalMessageContent(target, msg, x, y, maxWidth, clipHeight, draw, out _);

        float contentX = x + MentionPaddingX;
        float contentMaxWidth = Math.Max(maxWidth - MentionPaddingX * 2, 20f);

        float measuredBottom = DrawNormalMessageContent(
            target,
            msg,
            contentX,
            y,
            contentMaxWidth,
            clipHeight,
            draw: false,
            out float measuredWidth
        );

        if (draw)
        {

            float boxWidth = Math.Min(measuredWidth + MentionPaddingX * 2, maxWidth);

            var roundedRect = new RoundedRectangle
            {
                Rect = new Rect(x, y, boxWidth, measuredBottom - y),
                RadiusX = MentionCornerRadius,
                RadiusY = MentionCornerRadius,
            };

            _mentionBackgroundBrush ??= target.CreateSolidColorBrush(MentionBackgroundColor);
            _mentionBorderBrush ??= target.CreateSolidColorBrush(MentionBorderColor);
            target.FillRoundedRectangle(roundedRect, _mentionBackgroundBrush);
            target.DrawRoundedRectangle(roundedRect, _mentionBorderBrush, MentionBorderThickness);
        }

        return DrawNormalMessageContent(target, msg, contentX, y, contentMaxWidth, clipHeight, draw, out _);
    }

    private float DrawNormalMessageContent(
        ID2D1DCRenderTarget target,
        ChatMessage msg,
        float x,
        float y,
        float maxWidth,
        float clipHeight,
        bool draw,
        out float contentWidth
    )
    {
        string prefix = msg.IsAction ? "* " + msg.DisplayName + " " : msg.DisplayName + ": ";
        var usernameBrush = GetUsernameBrush(target, msg.Color);

        float badgesWidth = DrawBadges(target, msg.Badges, x, y, draw: false);
        float usernameX = x + badgesWidth;
        float usernameMaxWidth = Math.Max(maxWidth - badgesWidth, 20f);

        using var usernameLayout = DWriteFactory.CreateTextLayout(prefix, _usernameFormat!, usernameMaxWidth, UnboundedLayoutHeight);
        float usernameLineHeight = Math.Max((float)usernameLayout.Metrics.Height, badgesWidth > 0 ? BadgeSize : 0f);
        float badgesY = y + (usernameLineHeight - BadgeSize) / 2f;

        if (draw)
        {
            DrawBadges(target, msg.Badges, x, badgesY, draw: true);
            DrawTextWithOutline(target, new Vector2(usernameX, y), usernameLayout, usernameBrush);
        }
        float afterUsername = y + usernameLineHeight + UsernameToBodySpacing;
        float line1Width = badgesWidth + (float)usernameLayout.Metrics.Width;

        if (string.IsNullOrEmpty(msg.Text))
        {
            contentWidth = line1Width;
            return afterUsername;
        }

        if (afterUsername >= clipHeight)
        {
            contentWidth = line1Width;
            return afterUsername;
        }

        float bottom = DrawBody(target, msg, x, afterUsername, maxWidth, clipHeight, _bodyBrush!, out float bodyWidth, draw);
        contentWidth = Math.Max(line1Width, bodyWidth);
        return bottom;
    }

    private bool IsMentionOfChannel(string text)
    {
        if (string.IsNullOrEmpty(_settings.Channel) || string.IsNullOrEmpty(text))
            return false;

        if (_mentionRegex is null || _mentionRegexChannel != _settings.Channel)
        {
            _mentionRegex = new Regex(
                $@"@{Regex.Escape(_settings.Channel)}\b",
                RegexOptions.IgnoreCase
            );
            _mentionRegexChannel = _settings.Channel;
        }
        return _mentionRegex.IsMatch(text);
    }

    private static Color4 MentionBackgroundColor =>
        ThemeService.IsDark
            ? new Color4(0x91 / 255f, 0x47 / 255f, 0xFF / 255f, 0x4D / 255f)
            : new Color4(0x7A / 255f, 0x5C / 255f, 0xFF / 255f, 0x4D / 255f);

    private static Color4 MentionBorderColor =>
        ThemeService.IsDark
            ? new Color4(0x91 / 255f, 0x47 / 255f, 0xFF / 255f, 0xB3 / 255f)
            : new Color4(0x7A / 255f, 0x5C / 255f, 0xFF / 255f, 0xB3 / 255f);

    private float DrawBody(
        ID2D1DCRenderTarget target,
        ChatMessage msg,
        float x,
        float y,
        float maxWidth,
        float clipHeight,
        ID2D1Brush brush,
        out float contentWidth,
        bool draw = true,
        IDWriteTextFormat? format = null
    )
    {
        var fmt = format ?? _bodyFormat!;
        if (msg.Emotes.Count == 0)
        {
            var bodyLayout = GetOrCreateBodyLayout(msg, fmt, maxWidth);
            if (draw)
                DrawTextWithOutline(target, new Vector2(x, y), bodyLayout, brush);

            contentWidth = (float)bodyLayout.Metrics.Width;
            return y + (float)bodyLayout.Metrics.Height;
        }
        return DrawBodyWithEmotes(target, msg, x, y, maxWidth, clipHeight, brush, draw, out contentWidth, fmt);
    }

    private float DrawBodyWithEmotes(
        ID2D1DCRenderTarget target,
        ChatMessage msg,
        float x,
        float y,
        float maxWidth,
        float clipHeight,
        ID2D1Brush brush,
        bool draw,
        out float contentWidth,
        IDWriteTextFormat format
    )
    {
        float lineHeight = Math.Max(format.FontSize * BodyLineHeightFactor, EmoteSize);
        float cursorX = x;
        float cursorY = y;
        float maxLineWidth = 0f;

        foreach (var segment in SplitMessageIntoSegments(msg))
        {
            if (cursorY >= clipHeight)
                break;

            if (segment.IsEmote)
            {
                DrawEmoteToken(
                    target,
                    segment.Emote!,
                    x,
                    maxWidth,
                    lineHeight,
                    draw,
                    ref cursorX,
                    ref cursorY,
                    ref maxLineWidth
                );
                continue;
            }

            foreach (var word in SplitWords(segment.Text ?? ""))
                DrawWordToken(
                    target,
                    word,
                    x,
                    maxWidth,
                    lineHeight,
                    brush,
                    draw,
                    ref cursorX,
                    ref cursorY,
                    ref maxLineWidth,
                    format
                );
        }

        contentWidth = maxLineWidth;
        return cursorY + lineHeight;
    }

    private const int MaxCachedWordLayouts = 4000;
    private readonly Dictionary<(string Text, IDWriteTextFormat Format), IDWriteTextLayout> _wordLayoutCache = new();
    private readonly LinkedList<(string Text, IDWriteTextFormat Format)> _wordLayoutCacheOrder = new();

    private IDWriteTextLayout GetOrCreateWordLayout(string word, IDWriteTextFormat format)
    {
        var key = (word, format);
        if (_wordLayoutCache.TryGetValue(key, out var cached))
        {
            _wordLayoutCacheOrder.Remove(key);
            _wordLayoutCacheOrder.AddLast(key);
            return cached;
        }

        var layout = DWriteFactory.CreateTextLayout(word, format, float.MaxValue, float.MaxValue);
        _wordLayoutCache[key] = layout;
        _wordLayoutCacheOrder.AddLast(key);

        if (_wordLayoutCacheOrder.Count > MaxCachedWordLayouts)
        {
            var oldestKey = _wordLayoutCacheOrder.First!.Value;
            _wordLayoutCacheOrder.RemoveFirst();
            if (_wordLayoutCache.Remove(oldestKey, out var oldLayout))
                oldLayout.Dispose();
        }

        return layout;
    }

    private void InvalidateWordLayoutCache()
    {
        foreach (var layout in _wordLayoutCache.Values)
            layout.Dispose();
        _wordLayoutCache.Clear();
        _wordLayoutCacheOrder.Clear();
    }

    private void DrawWordToken(
        ID2D1DCRenderTarget target,
        string word,
        float x,
        float maxWidth,
        float lineHeight,
        ID2D1Brush brush,
        bool draw,
        ref float cursorX,
        ref float cursorY,
        ref float maxLineWidth,
        IDWriteTextFormat format
    )
    {
        if (word.Length == 0)
            return;

        var wordLayout = GetOrCreateWordLayout(word, format);
        float wordWidth = (float)wordLayout.Metrics.WidthIncludingTrailingWhitespace;

        if (cursorX + wordWidth > x + maxWidth && cursorX > x)
        {
            cursorX = x;
            cursorY += lineHeight;
        }

        if (draw)
            DrawTextWithOutline(target, new Vector2(cursorX, cursorY), wordLayout, brush);

        maxLineWidth = Math.Max(maxLineWidth, cursorX + (float)wordLayout.Metrics.Width - x);
        cursorX += wordWidth;
    }

    private static IEnumerable<string> SplitWords(string segment)
    {
        var parts = segment.Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
                continue;
            yield return i < parts.Length - 1 ? parts[i] + " " : parts[i];
        }
    }

    private static void DrawBitmapAt(
        ID2D1DCRenderTarget target,
        ID2D1Bitmap bitmap,
        float x,
        float y,
        float size
    )
    {
        var previousTransform = target.Transform;
        float scaleX = size / bitmap.Size.Width;
        float scaleY = size / bitmap.Size.Height;
        target.Transform =
            Matrix3x2.CreateScale(scaleX, scaleY) * Matrix3x2.CreateTranslation(x, y);
        target.DrawBitmap(bitmap);
        target.Transform = previousTransform;
    }
}