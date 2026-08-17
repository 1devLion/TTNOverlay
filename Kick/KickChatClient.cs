using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TTNOverlay.Models;
using TTNOverlay.Net.BrowserTls;
using TTNOverlay.Overlay;
using TTNOverlay.Services;

namespace TTNOverlay.Kick;

/// <summary>
/// WebSocket-based client for reading Kick chat messages in real time.
/// </summary>
/// <remarks>
/// This client connects to the same Pusher-hosted WebSocket used by Kick's web client.
/// It provides read‑only, unauthenticated access to public chatrooms.
/// </remarks>
public class KickChatClient : IKickChatClient
{
    private const string PusherWsUrl =
        "wss://ws-us2.pusher.com/app/32cbd69e4b950bf97679?protocol=7&client=js&version=8.4.0-rc2&flash=false";

    // User-Agent required to avoid Cloudflare blocking.
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
            + "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    private static readonly HttpClient Http = SharedHttpClient.Instance;

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private string _channelSlug = "";

    /// <summary>
    /// Subscriber tenure badge tiers for the current channel, sorted descending by month threshold.
    /// </summary>
    private List<(int Months, string Url)> _subscriberBadges = new();

    public event Action<ChatMessage>? MessageReceived;
    public event Action<string>? Connected;
    public event Action<string>? Disconnected;
    public event Action<Exception>? Error;

    /// <summary>
    /// Connects to the chat of the specified channel.
    /// </summary>
    /// <param name="channelSlug">The channel slug (username).</param>
    public async Task ConnectAsync(string channelSlug)
    {
        _channelSlug = channelSlug.Trim().ToLowerInvariant();
        DebugLog.Write($"KickChatClient: resolving chatroom id for '{_channelSlug}'...");

        var chatroomId = await ResolveChatroomAsync(_channelSlug);
        await FetchSubscriberBadgesAsync(_channelSlug);
        DebugLog.Write(
            $"KickChatClient: chatroom id = {chatroomId}, {_subscriberBadges.Count} subscriber badge tiers"
        );

        _cts = new CancellationTokenSource();
        _socket = new ClientWebSocket();

        await _socket.ConnectAsync(new Uri(PusherWsUrl), _cts.Token);
        DebugLog.Write($"KickChatClient: WebSocket connected, status: {_socket.State}");

        await SubscribeAsync($"chatrooms.{chatroomId}.v2");
        DebugLog.Write($"KickChatClient: subscribe sent for chatrooms.{chatroomId}.v2");

        Connected?.Invoke(_channelSlug);
        _ = ReceiveLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Fetches the channel's subscriber badge configuration from the Kick API.
    /// </summary>
    /// <param name="slug">The channel slug.</param>
    private async Task FetchSubscriberBadgesAsync(string slug)
    {
        try
        {
            string? json = await ImpersonatedHttpResolver.GetAsync("kick.com", $"/api/v2/channels/{slug}");
            if (json is null)
            {
                DebugLog.Write($"KickChatClient: Could not resolve /api/v2/channels/{slug} for subscriber_badges");
                return;
            }

            using var doc = JsonDocument.Parse(json);

            bool hasField = doc.RootElement.TryGetProperty("subscriber_badges", out var fieldEl);
            _subscriberBadges = ParseSubscriberBadges(doc.RootElement);

            if (!hasField)
            {
                var topLevelKeys = string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name));
                DebugLog.Write(
                    $"KickChatClient: 'subscriber_badges' is also not in /api/v2/channels/{slug}. " +
                    $"top-level keys available: [{topLevelKeys}]"
                );
            }
            else
            {
                DebugLog.Write(
                    $"KickChatClient: 'subscriber_badges' present in /api/v2/channels/{slug} " +
                    $"(kind={fieldEl.ValueKind}, {_subscriberBadges.Count} parsed tiers of " +
                    $"{(fieldEl.ValueKind == JsonValueKind.Array ? fieldEl.GetArrayLength() : 0)} raw entries)"
                );
            }
        }
        catch (JsonException ex)
        {
            DebugLog.Write($"KickChatClient: JSON from /api/v2/channels/{slug} not parseable ({ex.Message})");
        }
    }

    /// <summary>
    /// Resolves the numeric chatroom ID for a given channel slug.
    /// </summary>
    /// <param name="slug">The channel slug.</param>
    /// <returns>The chatroom ID, or null if resolution fails.</returns>
    public async Task<int?> ResolveChatroomAsync(string slug)
    {
        string? json = await ImpersonatedHttpResolver.GetAsync("kick.com", $"/api/v2/channels/{slug}/chatroom");
        if (json == null)
        {
            DebugLog.Write($"KickChatClient: Could not resolve chatroom for '{slug}'");
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var id))
                return id;
        }
        catch (JsonException ex)
        {
            DebugLog.Write($"KickChatClient: Complete non-parseable JSON ({ex.Message}), using regex fallback");
        }

        var match = Regex.Match(json, "\"id\"\\s*:\\s*(\\d+)");
        if (!match.Success)
        {
            DebugLog.Write($"KickChatClient: response with no recognizable 'id' field for '{slug}'");
            return null;
        }

        return int.Parse(match.Groups[1].Value);
    }

    /// <summary>
    /// Parses the "subscriber_badges" array from the channel endpoint.
    /// </summary>
    /// <param name="root">The root JSON element of the channel response.</param>
    /// <returns>A list of (months threshold, badge image URL) sorted descending by months.</returns>
    private static List<(int Months, string Url)> ParseSubscriberBadges(JsonElement root)
    {
        var result = new List<(int Months, string Url)>();

        if (
            !root.TryGetProperty("subscriber_badges", out var badgesEl)
            || badgesEl.ValueKind != JsonValueKind.Array
        )
            return result;

        foreach (var entry in badgesEl.EnumerateArray())
        {
            if (
                !entry.TryGetProperty("months", out var monthsEl)
                || !entry.TryGetProperty("badge_image", out var imageEl)
                || !imageEl.TryGetProperty("src", out var srcEl)
            )
                continue;

            var src = srcEl.GetString();
            if (string.IsNullOrEmpty(src))
                continue;

            result.Add((monthsEl.GetInt32(), src));
        }

        result.Sort((a, b) => b.Months.CompareTo(a.Months));
        return result;
    }

    /// <summary>
    /// Returns the URL of the highest subscriber badge tier the user qualifies for, or null.
    /// </summary>
    /// <param name="months">The user's tenure in months.</param>
    private string? ResolveSubscriberBadgeUrl(int months)
    {
        foreach (var (tierMonths, url) in _subscriberBadges)
        {
            if (months >= tierMonths)
                return url;
        }
        return null;
    }

    /// <summary>
    /// Fixed sub_gifter tier thresholds (local icons available for these levels).
    /// </summary>
    private static readonly int[] GifterTierThresholds = { 200, 50, 10, 5, 1 };

    /// <summary>
    /// Returns the highest gifter tier the user qualifies for, or null.
    /// </summary>
    private static int? ResolveGifterTier(int giftCount)
    {
        foreach (var tier in GifterTierThresholds)
        {
            if (giftCount >= tier)
                return tier;
        }
        return null;
    }

    private Task SubscribeAsync(string channel)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                @event = "pusher:subscribe",
                data = new { auth = "", channel },
            }
        );
        return SendAsync(payload);
    }

    private async Task SendAsync(string message)
    {
        if (_socket is null)
            return;
        var bytes = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[16384];
        var sb = new StringBuilder();

        try
        {
            while (_socket is { State: WebSocketState.Open } && !token.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(buffer, token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    DebugLog.Write("KickChatClient: socket closed by the server");
                    Disconnected?.Invoke("closed by server");
                    break;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage)
                    continue;

                var frame = sb.ToString();
                sb.Clear();

                await HandleFrameAsync(frame);
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            DebugLog.WriteException("KickChatClient.ReceiveLoopAsync", ex);
            Error?.Invoke(ex);
        }
    }

    private async Task HandleFrameAsync(string frame)
    {
        DebugLog.Write($"Kick RECV: {(frame.Length > 2000 ? frame[..2000] + "..." : frame)}");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(frame);
        }
        catch (JsonException ex)
        {
            DebugLog.WriteException("KickChatClient.HandleFrameAsync (parse frame)", ex);
            return;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("event", out var eventElement))
                return;

            switch (eventElement.GetString())
            {
                case "pusher:ping":
                    await SendAsync("{\"event\":\"pusher:pong\",\"data\":{}}");
                    return;

                case "pusher:connection_established":
                    DebugLog.Write("KickChatClient: pusher connection established");
                    return;

                case "pusher_internal:subscription_succeeded":
                    DebugLog.Write("KickChatClient: subscription succeeded");
                    return;

                case "App\\Events\\ChatMessageEvent":
                case "App\\Events\\ChatMessageSentEvent":
                    HandleChatMessageEvent(doc.RootElement);
                    return;
            }
        }
    }

    private void HandleChatMessageEvent(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var dataElement))
            return;

        var dataRaw = dataElement.GetString();
        if (string.IsNullOrEmpty(dataRaw))
            return;

        using var dataDoc = JsonDocument.Parse(dataRaw);
        var data = dataDoc.RootElement;

        var rawContent = data.TryGetProperty("content", out var contentEl)
            ? contentEl.GetString() ?? ""
            : "";
        var (text, emotes) = ParseKickEmotes(rawContent);

        var username = "";
        var displayName = "";
        var color = ChatColors.DefaultUserGray;
        var badges = new List<Badge>();

        if (data.TryGetProperty("sender", out var sender))
        {
            username = sender.TryGetProperty("slug", out var slugEl) ? slugEl.GetString() ?? "" : "";
            displayName = sender.TryGetProperty("username", out var unameEl)
                ? unameEl.GetString() ?? ""
                : username;

            if (sender.TryGetProperty("identity", out var identity))
            {
                if (
                    identity.TryGetProperty("color", out var colorEl)
                    && !string.IsNullOrEmpty(colorEl.GetString())
                )
                    color = colorEl.GetString()!;

                if (
                    identity.TryGetProperty("badges", out var badgesEl)
                    && badgesEl.ValueKind == JsonValueKind.Array
                )
                {
                    foreach (var badgeEl in badgesEl.EnumerateArray())
                    {
                        var type = badgeEl.TryGetProperty("type", out var typeEl)
                            ? typeEl.GetString()
                            : null;
                        if (string.IsNullOrEmpty(type))
                            continue;

                        if (type == "subscriber")
                        {
                            var count =
                                badgeEl.TryGetProperty("count", out var countEl)
                                && countEl.TryGetInt32(out var parsedCount)
                                    ? parsedCount
                                    : 0;
                            var subUrl = ResolveSubscriberBadgeUrl(count);
                            badges.Add(
                                new Badge
                                {
                                    Name = type,
                                    Version = count.ToString(),
                                    IconUrl = subUrl,
                                    LocalIcon = subUrl is null && KickBadgeIconLoader.HasIcon(type)
                                        ? type
                                        : null,
                                }
                            );
                        }
                        else if (type == "sub_gifter")
                        {
                            var count =
                                badgeEl.TryGetProperty("count", out var giftCountEl)
                                && giftCountEl.TryGetInt32(out var parsedGiftCount)
                                    ? parsedGiftCount
                                    : 0;
                            var tier = ResolveGifterTier(count);
                            badges.Add(
                                new Badge
                                {
                                    Name = type,
                                    Version = count.ToString(),
                                    LocalIcon = tier is null ? null : $"sub_gifter_{tier}",
                                }
                            );
                        }
                        else
                        {
                            badges.Add(
                                new Badge
                                {
                                    Name = type,
                                    Version = "",
                                    LocalIcon = KickBadgeIconLoader.HasIcon(type) ? type : null,
                                }
                            );
                        }
                    }
                }

                if (
                    identity.TryGetProperty("badges_v2", out var badgesV2El)
                    && badgesV2El.ValueKind == JsonValueKind.Array
                )
                {
                    foreach (var badgeEl in badgesV2El.EnumerateArray())
                    {
                        var name = badgeEl.TryGetProperty("name", out var nameEl)
                            ? nameEl.GetString()
                            : null;
                        var imageUrl = badgeEl.TryGetProperty("image_url", out var urlEl)
                            ? urlEl.GetString()
                            : null;
                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(imageUrl))
                            continue;

                        var version =
                            badgeEl.TryGetProperty("metadata", out var metaEl)
                            && metaEl.TryGetProperty("level", out var levelEl)
                            && levelEl.TryGetInt32(out var level)
                                ? level.ToString()
                                : "";

                        badges.Add(
                            new Badge
                            {
                                Name = name,
                                Version = version,
                                IconUrl = imageUrl,
                            }
                        );
                    }
                }
            }
        }

        MessageReceived?.Invoke(
            new ChatMessage
            {
                Username = username,
                DisplayName = string.IsNullOrEmpty(displayName) ? username : displayName,
                Color = color,
                Text = text,
                Badges = badges,
                Emotes = emotes,
            }
        );
    }

    private static readonly Regex EmoteTokenPattern = new(
        @"\[emote:(\d+):([^\]]+)\]",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Parses Kick's inline emote tokens ("[emote:ID:NAME]") from the raw message text.
    /// </summary>
    /// <param name="raw">The raw message content from Kick.</param>
    /// <returns>
    /// A tuple containing the plain text (with tokens replaced by their names) and a list of
    /// <see cref="EmotePosition"/> objects that describe the emote placements.
    /// </returns>
    internal static (string Text, List<EmotePosition> Emotes) ParseKickEmotes(string raw)
    {
        var emotes = new List<EmotePosition>();
        var sb = new StringBuilder();
        var lastIndex = 0;

        foreach (Match match in EmoteTokenPattern.Matches(raw))
        {
            sb.Append(raw, lastIndex, match.Index - lastIndex);

            var start = sb.Length;
            var id = match.Groups[1].Value;
            var name = match.Groups[2].Value;
            sb.Append(name);
            var end = sb.Length - 1;

            emotes.Add(
                new EmotePosition
                {
                    Id = id,
                    Start = start,
                    End = end,
                    Source = EmoteSource.Kick,
                    StaticUrl = $"https://files.kick.com/emotes/{id}/fullsize",
                    AnimatedUrl = $"https://files.kick.com/emotes/{id}/fullsize",
                }
            );

            lastIndex = match.Index + match.Length;
        }

        sb.Append(raw, lastIndex, raw.Length - lastIndex);
        return (sb.ToString(), emotes);
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_socket is { State: WebSocketState.Open })
        {
            try
            {
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "bye",
                    CancellationToken.None
                );
            }
            catch
            {
                // Best-effort close; nothing to do if the socket is already gone.
            }
        }
        _socket?.Dispose();
    }
}