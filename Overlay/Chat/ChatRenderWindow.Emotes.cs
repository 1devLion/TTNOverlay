using TTNOverlay.Models;
using TTNOverlay.Services;
using TTNOverlay.Twitch;
using Vortice.Direct2D1;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: resolves emotes (Twitch and third-party) referenced in incoming messages.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private IHelixClient? _helix;

    private int DecodeTargetSize(int displayPx) => _settings.HighQualityMedia ? 0 : displayPx;

    private Dictionary<string, string>? _badgeUrls;

    private Dictionary<string, ThirdPartyEmoteProvider.ResolvedEmote>? _thirdPartyEmotes;

    private readonly HashSet<string> _loggedWaitingBadges = new();

    private async Task LoadBadgeMapAsync(string channelLogin)
    {
        if (!_settings.EnableTwitchApi || !_settings.ShowBadges)
            return;

        _helix ??= new HelixClient(TwitchAuthService.ClientId);
        _moderation ??= new ModerationService(_settings);

        var token = await _moderation.GetAccessTokenAsync();
        if (token is null)
        {
            DebugLog.Write("LoadBadgeMapAsync: no Twitch session, skipping badge fetch");
            return;
        }

        var map = await _helix.GetBadgeMapAsync(channelLogin, token);
        DebugLog.Write($"LoadBadgeMapAsync: {(map is null ? "failed" : $"{map.Count} badges")}");

        PostToUiThread(() =>
        {
            _badgeUrls = map;
            RequestRender();
        });
    }

    private async Task LoadThirdPartyEmotesAsync(string channelLogin)
    {
        if (string.IsNullOrWhiteSpace(channelLogin))
            return;

        string? token = null;
        if (_settings.EnableTwitchApi)
        {
            _moderation ??= new ModerationService(_settings);
            token = await _moderation.GetAccessTokenAsync();
        }

        var map = await ThirdPartyEmoteProvider.LoadForChannelAsync(
            channelLogin,
            TwitchAuthService.ClientId,
            token
        );
        DebugLog.Write($"LoadThirdPartyEmotesAsync: {map.Count} emotes de terceros resueltos");

        PostToUiThread(() =>
        {
            _thirdPartyEmotes = map;
            RequestRender();
        });
    }

    private void AugmentWithThirdPartyEmotes(ChatMessage msg)
    {
        if (
            !_settings.EnableThirdPartyEmotes
            || _thirdPartyEmotes is null
            || _thirdPartyEmotes.Count == 0
        )
            return;

        var text = msg.Text;
        int cursor = 0;

        while (cursor < text.Length)
        {
            while (cursor < text.Length && text[cursor] == ' ')
                cursor++;

            int start = cursor;
            while (cursor < text.Length && text[cursor] != ' ')
                cursor++;

            if (cursor == start)
                continue;

            var word = text[start..cursor];
            if (
                _thirdPartyEmotes.TryGetValue(word, out var emote)
                && !msg.Emotes.Any(e => e.Start <= cursor - 1 && e.End >= start)
            )
            {
                msg.Emotes.Add(
                    new EmotePosition
                    {
                        Id = emote.Id,
                        Start = start,
                        End = cursor - 1,
                        Source = emote.Source,
                        StaticUrl = emote.StaticUrl,
                        AnimatedUrl = emote.AnimatedUrl,
                    }
                );
            }
        }
    }

    private float DrawBadges(ID2D1DCRenderTarget target, List<Badge> badges, float x, float y, bool draw = true)
    {
        float cursor = 0f;
        foreach (var badge in badges)
        {
            string key = $"badge:{badge.Name}/{badge.Version}";
            if (
                _badgeUrls is null
                || !_badgeUrls.TryGetValue($"{badge.Name}/{badge.Version}", out var url)
            )
            {
                if (_loggedWaitingBadges.Add(key))
                    DebugLog.Write(
                        $"badges not loaded yet or no match for {badge.Name}/{badge.Version}"
                    );
                continue;
            }

            var bitmap = GetOrLoadImageBitmap(key, url, DecodeTargetSize((int)BadgeSize));
            if (bitmap is null)
            {
                if (_loggedWaitingBadges.Add(key))
                    DebugLog.Write(
                        $"DrawBadges: bitmap not available yet for {key}, no se repetirá este log hasta que resuelva"
                    );
                continue;
            }

            if (draw)
                DrawBitmapAt(target, bitmap, x + cursor, y, BadgeSize);
            cursor += BadgeSize + BadgeSpacing;
        }
        return cursor;
    }

    private void DrawEmoteToken(
        ID2D1DCRenderTarget target,
        EmotePosition emote,
        float x,
        float maxWidth,
        float lineHeight,
        bool draw,
        ref float cursorX,
        ref float cursorY,
        ref float maxLineWidth
    )
    {
        if (cursorX + EmoteSize > x + maxWidth && cursorX > x)
        {
            cursorX = x;
            cursorY += lineHeight;
        }

        TryLoadAnimatedEmote(emote);

        string emoteKey = EmoteCacheKey(emote);
        ID2D1Bitmap? bitmap;
        if (_animatedImageCache.TryGetValue(emoteKey, out var animFrames) && animFrames is not null)
            bitmap = animFrames[
                _animationState.TryGetValue(emoteKey, out var st) ? st.Index : 0
            ].Bitmap;
        else
            bitmap = GetOrLoadEmoteBitmap(emote);

        if (draw && bitmap is not null)
        {
            float emoteY = cursorY + (lineHeight - EmoteSize) / 2f;
            DrawBitmapAt(target, bitmap, cursorX, emoteY, EmoteSize);
        }

        cursorX += EmoteSize + EmoteSpacing;
        maxLineWidth = Math.Max(maxLineWidth, cursorX - EmoteSpacing - x);
    }

    private static string EmoteCacheKey(EmotePosition emote) => $"emote:{emote.Source}:{emote.Id}";

    private ID2D1Bitmap? GetOrLoadEmoteBitmap(EmotePosition emote)
    {
        string key = EmoteCacheKey(emote);
        string? url =
            emote.Source == EmoteSource.Twitch
                ? EmoteUrlProvider.GetUrl(emote.Id, EmoteUrlProvider.PickSizeBucket((int)EmoteSize))
                : emote.StaticUrl;

        if (string.IsNullOrEmpty(url))
            return null;

        return GetOrLoadImageBitmap(key, url, DecodeTargetSize((int)EmoteSize));
    }

    private void TryLoadAnimatedEmote(EmotePosition emote)
    {
        string key = EmoteCacheKey(emote);

        string? animatedUrl =
            emote.Source == EmoteSource.Twitch
                ? EmoteUrlProvider.GetAnimatedUrl(emote.Id)
                : emote.AnimatedUrl;

        if (animatedUrl is null)
            return;

        string animatedCacheKey = $"{emote.Source}:{emote.Id}";

        TryLoadAnimatedImage(key, animatedCacheKey, animatedUrl, EmoteSize);
    }
}