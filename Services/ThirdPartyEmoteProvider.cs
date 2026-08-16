using System.Text.Json;
using TTNOverlay.Models;
using TTNOverlay.Twitch;

namespace TTNOverlay.Services;

/// <summary>
/// Resolves third-party emote sets (BTTV/FFZ/7TV) for a channel and maps emote codes found in chat text.
/// </summary>
public static class ThirdPartyEmoteProvider
{
    private static readonly HttpClient Http = SharedHttpClient.Instance;

    public record ResolvedEmote(
        string Id,
        EmoteSource Source,
        string StaticUrl,
        string? AnimatedUrl
    );

    public static async Task<Dictionary<string, ResolvedEmote>> LoadForChannelAsync(
        string channelLogin,
        string? twitchClientId,
        string? userAccessToken = null
    )
    {
        var map = new Dictionary<string, ResolvedEmote>();

        await LoadBttvAsync(map, "https://api.betterttv.net/3/cached/emotes/global");
        await LoadFfzGenericAsync(map, "https://api.frankerfacez.com/v1/set/global");

        await LoadFfzGenericAsync(
            map,
            $"https://api.frankerfacez.com/v1/room/{Uri.EscapeDataString(channelLogin)}"
        );

        await LoadSevenTvAsync(map, "https://7tv.io/v3/emote-sets/global", nested: false);

        if (!string.IsNullOrWhiteSpace(twitchClientId) && !string.IsNullOrWhiteSpace(userAccessToken))
        {
            var helix = new HelixClient(twitchClientId);
            var userId = await helix.GetUserIdByLoginAsync(channelLogin, userAccessToken);
            if (userId != null)
            {
                await LoadBttvAsync(
                    map,
                    $"https://api.betterttv.net/3/cached/users/twitch/{userId}"
                );
                await LoadSevenTvAsync(
                    map,
                    $"https://7tv.io/v3/users/twitch/{userId}",
                    nested: true
                );
            }
        }

        return map;
    }

    private static async Task LoadBttvAsync(Dictionary<string, ResolvedEmote> map, string url)
    {
        try
        {
            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                AddBttvArray(map, doc.RootElement);
            }
            else
            {
                if (doc.RootElement.TryGetProperty("channelEmotes", out var ch))
                    AddBttvArray(map, ch);
                if (doc.RootElement.TryGetProperty("sharedEmotes", out var sh))
                    AddBttvArray(map, sh);
            }
        }
        catch (Exception ex)
        {
            DebugLog.WriteException($"ThirdPartyEmoteProvider.LoadBttvAsync ({url})", ex);
        }
    }

    private static void AddBttvArray(Dictionary<string, ResolvedEmote> map, JsonElement arr)
    {
        foreach (var e in arr.EnumerateArray())
        {
            var id = e.GetProperty("id").GetString();
            var code = e.GetProperty("code").GetString();
            if (id is null || code is null)
                continue;

            bool animated =
                e.TryGetProperty("animated", out var a) && a.ValueKind == JsonValueKind.True;
            var url = $"https://cdn.betterttv.net/emote/{id}/2x";
            map[code] = new ResolvedEmote(id, EmoteSource.Bttv, url, animated ? url : null);
        }
    }

    private static async Task LoadFfzGenericAsync(Dictionary<string, ResolvedEmote> map, string url)
    {
        try
        {
            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("sets", out var sets))
                return;

            foreach (var set in sets.EnumerateObject())
                AddFfzSet(map, set.Value);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException($"ThirdPartyEmoteProvider.LoadFfzGenericAsync ({url})", ex);
        }
    }

    private static void AddFfzSet(Dictionary<string, ResolvedEmote> map, JsonElement set)
    {
        if (!set.TryGetProperty("emoticons", out var emotes))
            return;

        foreach (var e in emotes.EnumerateArray())
        {
            var name = e.GetProperty("name").GetString();
            if (name is null || !e.TryGetProperty("urls", out var urls))
                continue;

            var staticUrl = BestFfzUrl(urls);
            if (staticUrl is null)
                continue;

            string? animatedUrl = e.TryGetProperty("animated", out var anim)
                ? BestFfzUrl(anim)
                : null;
            var id = e.TryGetProperty("id", out var idEl) ? idEl.ToString() : name;

            map[name] = new ResolvedEmote(id, EmoteSource.Ffz, staticUrl, animatedUrl);
        }
    }

    private static string? BestFfzUrl(JsonElement urls)
    {
        foreach (var key in new[] { "2", "4", "1" })
            if (urls.TryGetProperty(key, out var val) && val.GetString() is { } s)
                return s.StartsWith("//") ? "https:" + s : s;
        return null;
    }

    private static async Task LoadSevenTvAsync(
        Dictionary<string, ResolvedEmote> map,
        string url,
        bool nested
    )
    {
        try
        {
            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            if (nested)
            {
                if (
                    !root.TryGetProperty("emote_set", out var set)
                    || !set.TryGetProperty("emotes", out var nestedEmotes)
                )
                    return;
                AddSevenTvArray(map, nestedEmotes);
            }
            else if (root.TryGetProperty("emotes", out var emotes))
            {
                AddSevenTvArray(map, emotes);
            }
        }
        catch (Exception ex)
        {
            DebugLog.WriteException($"ThirdPartyEmoteProvider.LoadSevenTvAsync ({url})", ex);
        }
    }

    private static void AddSevenTvArray(Dictionary<string, ResolvedEmote> map, JsonElement emotes)
    {
        foreach (var e in emotes.EnumerateArray())
        {
            var name = e.GetProperty("name").GetString();
            if (
                name is null
                || !e.TryGetProperty("data", out var data)
                || !data.TryGetProperty("host", out var host)
            )
                continue;

            var baseUrl = host.GetProperty("url").GetString();
            if (baseUrl is null)
                continue;
            if (baseUrl.StartsWith("//"))
                baseUrl = "https:" + baseUrl;

            bool animated =
                data.TryGetProperty("animated", out var a) && a.ValueKind == JsonValueKind.True;
            var id = e.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? name : name;
            var emoteUrl = $"{baseUrl}/2x.webp";

            map[name] = new ResolvedEmote(
                id,
                EmoteSource.SevenTv,
                emoteUrl,
                animated ? emoteUrl : null
            );
        }
    }
}