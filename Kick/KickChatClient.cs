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
/// This client connects to the same realtime WebSocket (Centrifugo) used by Kick's web client.
/// It provides read‑only, unauthenticated access to public chatrooms.
/// </remarks>
public class KickChatClient : IKickChatClient
{
    // Since October 2026 Kick serves chat over Centrifugo (the Pusher app key was retired: it answers 4001). Connecting
    // takes three steps, all of which Kick only accepts when they look like they come from kick.com (Origin header):
    //   1. POST channels/{channelId}/chat/connection -> which realtime server carries this channel's chat
    //   2. POST auth/connection                       -> a guest token (45 min) for a client id we make up
    //   3. WebSocket to that server, "connect" with the token, then "subscribe" to chatrooms.{chatroomId}.v2
    private const string RealtimeHost = "web.kick.com";
    private const string RealtimeApiPath = "/api/v1/realtime";
    private const string KickOrigin = "https://kick.com";

    /// <summary>How much longer than the ping interval Kick may stay silent before the connection counts as dead.</summary>
    private static readonly TimeSpan SilenceGrace = TimeSpan.FromSeconds(20);

    // User-Agent required to avoid Cloudflare blocking.
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
            + "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    private static readonly HttpClient Http = SharedHttpClient.Instance;

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private string _channelSlug = "";

    /// <summary>Any id; Kick wants the same one for the connection request and for every token it gives us.</summary>
    private readonly string _clientId = Guid.NewGuid().ToString();

    private int? _channelId;
    private string _chatChannel = "";
    private int _commandId;
    private readonly Dictionary<int, string> _pendingCommands = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _pingSeconds = 25;
    private DateTime? _refreshAtUtc;
    private bool _refreshAsked;

    /// <summary>
    /// Subscriber tenure badge tiers for the current channel, sorted descending by month threshold.
    /// </summary>
    private List<(int Months, string Url)> _subscriberBadges = new();

    public event Action<ChatMessage>? MessageReceived;
    public event Action<string>? Connected;
    public event Action<string>? Disconnected;
    public event Action<Exception>? Error;
    public event Action<string>? MessageDeleted;
    public event Action<KickBanEvent>? UserBanned;
    public event Action<string>? UserUnbanned;
    public event Action? ChatCleared;

    /// <summary>
    /// Connects to the chat of the specified channel.
    /// </summary>
    /// <param name="channelSlug">The channel slug (username).</param>
    public async Task ConnectAsync(string channelSlug)
    {
        // A previous connection of this client (reconnect): it must not report anything anymore.
        _cts?.Cancel();
        _socket?.Dispose();
        _socket = null;

        _channelSlug = channelSlug.Trim().ToLowerInvariant();
        _channelId = null;
        DebugLog.Write($"KickChatClient: resolving chatroom id for '{_channelSlug}'...");

        var chatroomId = await ResolveChatroomAsync(_channelSlug);
        await FetchSubscriberBadgesAsync(_channelSlug);
        DebugLog.Write(
            $"KickChatClient: chatroom id = {chatroomId}, channel id = {_channelId}, {_subscriberBadges.Count} subscriber badge tiers"
        );

        if (chatroomId is null)
            throw new InvalidOperationException($"Could not find the Kick chatroom of '{_channelSlug}'.");
        if (_channelId is null)
            throw new InvalidOperationException($"Could not find the Kick channel id of '{_channelSlug}'.");

        string wsUrl = await ResolveRealtimeUrlAsync(_channelId.Value);
        string token = await FetchRealtimeTokenAsync();

        _chatChannel = $"chatrooms.{chatroomId}.v2";
        lock (_pendingCommands)
            _pendingCommands.Clear();
        _commandId = 0;
        _pingSeconds = 25;
        _refreshAtUtc = null;
        _refreshAsked = false;

        var cts = new CancellationTokenSource();
        var socket = new ClientWebSocket();
        _cts = cts;
        _socket = socket;

        // The server refuses the handshake (403) unless the Origin is kick.com.
        socket.Options.SetRequestHeader("Origin", KickOrigin);
        try
        {
            socket.Options.SetRequestHeader("User-Agent", BrowserUserAgent);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"KickChatClient: could not set the User-Agent of the socket ({ex.Message})");
        }

        await socket.ConnectAsync(new Uri(wsUrl), cts.Token);
        DebugLog.Write($"KickChatClient: WebSocket connected to {wsUrl}, status: {socket.State}");

        _ = ReceiveLoopAsync(socket, cts.Token);
        await SendCommandAsync("connect", $"\"connect\":{{\"token\":{JsonSerializer.Serialize(token)},\"name\":\"js\"}}");
        DebugLog.Write("KickChatClient: connect command sent");
    }

    /// <summary>Step 1: which realtime server carries the chat of this channel.</summary>
    private async Task<string> ResolveRealtimeUrlAsync(int channelId)
    {
        string body =
            $"{{\"client\":{{\"id\":\"{_clientId}\",\"type\":\"web\"}},\"capabilities\":{{\"accepted_providers\":[{{\"provider\":\"centrifugo\"}}]}}}}";
        var (status, json) = await ImpersonatedHttpResolver.PostJsonAsync(
            RealtimeHost,
            $"{RealtimeApiPath}/channels/{channelId}/chat/connection",
            body,
            KickOrigin
        );
        if (status != 200 || json is null)
            throw new InvalidOperationException($"Kick refused the realtime connection request (HTTP {status}): {Shorten(json)}");

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (
                doc.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("connections", out var connections)
                && connections.ValueKind == JsonValueKind.Array
            )
            {
                var offered = new List<string>();
                foreach (var connection in connections.EnumerateArray())
                {
                    string provider = connection.TryGetProperty("provider", out var providerEl) ? providerEl.GetString() ?? "" : "";
                    offered.Add(provider);
                    if (
                        provider == "centrifugo"
                        && connection.TryGetProperty("credentials", out var credentials)
                        && credentials.TryGetProperty("url", out var urlEl)
                        && urlEl.GetString() is { Length: > 0 } url
                    )
                    {
                        DebugLog.Write($"KickChatClient: realtime server = {url}");
                        return url;
                    }
                }
                throw new InvalidOperationException($"Kick offered no Centrifugo server (offered: {string.Join(", ", offered)}).");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"The realtime connection answer of Kick is not JSON: {ex.Message}");
        }

        throw new InvalidOperationException($"The realtime connection answer of Kick has no connections: {Shorten(json)}");
    }

    /// <summary>Step 2: a guest token. It lasts 45 minutes; a new one for the same client id keeps the connection alive.</summary>
    private async Task<string> FetchRealtimeTokenAsync()
    {
        var (status, json) = await ImpersonatedHttpResolver.PostJsonAsync(
            RealtimeHost,
            $"{RealtimeApiPath}/auth/connection",
            $"{{\"client_id\":\"{_clientId}\"}}",
            KickOrigin
        );
        if (status != 200 || json is null)
            throw new InvalidOperationException($"Kick refused the realtime token request (HTTP {status}): {Shorten(json)}");

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (
                doc.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("token", out var tokenEl)
                && tokenEl.GetString() is { Length: > 0 } token
            )
                return token;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"The realtime token answer of Kick is not JSON: {ex.Message}");
        }

        throw new InvalidOperationException($"The realtime token answer of Kick has no token: {Shorten(json)}");
    }

    private static string Shorten(string? text) =>
        text is null ? "(no answer)" : text.Length > 300 ? text[..300] + "..." : text;

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

            // The realtime connection is asked for by channel id (not the chatroom id).
            if (doc.RootElement.TryGetProperty("id", out var channelIdEl) && channelIdEl.TryGetInt32(out int channelId))
                _channelId = channelId;

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

    /// <inheritdoc />
    public async Task<int?> GetViewerCountAsync()
    {
        if (string.IsNullOrEmpty(_channelSlug))
            return null;

        string? json = await ImpersonatedHttpResolver.GetAsync("kick.com", $"/api/v2/channels/{_channelSlug}");
        if (json is null)
        {
            DebugLog.Write($"KickChatClient.GetViewerCountAsync: could not resolve /api/v2/channels/{_channelSlug}");
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);

            // "livestream" is null (or absent) when the channel is offline — that's the expected,
            // common case, not an error, so no viewer count is neither a bug nor worth a log line.
            if (
                doc.RootElement.TryGetProperty("livestream", out var live)
                && live.ValueKind == JsonValueKind.Object
                && live.TryGetProperty("viewer_count", out var viewerCountEl)
            )
            {
                return viewerCountEl.ValueKind switch
                {
                    JsonValueKind.Number => (int)viewerCountEl.GetDouble(),
                    _ => null,
                };
            }

            return null;
        }
        catch (JsonException ex)
        {
            DebugLog.Write(
                $"KickChatClient.GetViewerCountAsync: JSON from /api/v2/channels/{_channelSlug} not parseable ({ex.Message})"
            );
            return null;
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
            if (
                doc.RootElement.TryGetProperty("channel_id", out var channelIdEl)
                && channelIdEl.TryGetInt32(out var chatroomChannelId)
            )
                _channelId ??= chatroomChannelId;

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

    private async Task SendAsync(string message)
    {
        var socket = _socket;
        if (socket is not { State: WebSocketState.Open })
            return;

        var bytes = Encoding.UTF8.GetBytes(message);

        // A socket takes one send at a time, and the receive loop (pings) and the token refresh both send.
        await _sendLock.WaitAsync();
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Sends a Centrifugo command; <paramref name="bodyFragment"/> is the JSON after the id, e.g. "subscribe":{...}.</summary>
    private Task SendCommandAsync(string kind, string bodyFragment)
    {
        int id = Interlocked.Increment(ref _commandId);
        lock (_pendingCommands)
            _pendingCommands[id] = kind;
        return SendAsync($"{{\"id\":{id},{bodyFragment}}}");
    }

    /// <summary>Reports that this connection is over, unless it is an old one that was already replaced.</summary>
    private void NotifyDisconnected(ClientWebSocket socket, string reason)
    {
        if (!ReferenceEquals(_socket, socket))
            return;

        DebugLog.Write($"KickChatClient: disconnected ({reason})");
        _refreshAtUtc = null;
        try
        {
            socket.Abort();
        }
        catch
        {
            // Already gone.
        }
        Disconnected?.Invoke(reason);
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[16384];
        var sb = new StringBuilder();

        try
        {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                // Kick pings every 25 s: much longer than that without a word means the connection died silently.
                using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                receiveCts.CancelAfter(TimeSpan.FromSeconds(_pingSeconds) + SilenceGrace);

                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), receiveCts.Token);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    NotifyDisconnected(socket, "connection went quiet");
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    DebugLog.Write(
                        $"KickChatClient: socket closed by the server ({socket.CloseStatus} {socket.CloseStatusDescription})"
                    );
                    NotifyDisconnected(socket, "closed by server");
                    return;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage)
                    continue;

                var frame = sb.ToString();
                sb.Clear();

                await HandleRealtimeFrameAsync(socket, frame);
                StartTokenRefreshIfDue(socket);
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            DebugLog.WriteException("KickChatClient.ReceiveLoopAsync", ex);
            if (ReferenceEquals(_socket, socket))
                Error?.Invoke(ex);
        }
    }

    /// <summary>A text frame of the Centrifugo protocol: one or several JSON lines, or "{}" (the server's ping).</summary>
    private async Task HandleRealtimeFrameAsync(ClientWebSocket socket, string frame)
    {
        if (frame.Trim() != "{}")
            DebugLog.Write($"Kick RECV: {(frame.Length > 2000 ? frame[..2000] + "..." : frame)}");

        foreach (var rawLine in frame.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (line == "{}")
            {
                await SendAsync("{}");
                continue;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(line);
            }
            catch (JsonException ex)
            {
                DebugLog.WriteException("KickChatClient.HandleRealtimeFrameAsync (parse line)", ex);
                continue;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    continue;

                if (root.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out int id) && id > 0)
                    await HandleCommandReplyAsync(socket, id, root);
                else if (root.TryGetProperty("push", out var push))
                    await HandlePushAsync(socket, push);
            }
        }
    }

    private async Task HandleCommandReplyAsync(ClientWebSocket socket, int id, JsonElement reply)
    {
        string? kind;
        lock (_pendingCommands)
        {
            if (!_pendingCommands.Remove(id, out kind))
                return;
        }

        string? error = null;
        if (reply.TryGetProperty("error", out var errorEl) && errorEl.ValueKind == JsonValueKind.Object)
        {
            int code = errorEl.TryGetProperty("code", out var codeEl) && codeEl.TryGetInt32(out int c) ? c : 0;
            string message = errorEl.TryGetProperty("message", out var messageEl) ? messageEl.GetString() ?? "" : "";
            error = $"{code} {message}";
        }

        switch (kind)
        {
            case "connect":
                if (error is not null)
                {
                    DebugLog.Write($"KickChatClient: Kick refused the connection: {error}");
                    NotifyDisconnected(socket, "refused: " + error);
                    return;
                }

                ScheduleTokenRefresh(reply.TryGetProperty("connect", out var connectEl) ? connectEl : default);
                DebugLog.Write($"KickChatClient: connected (ping every {_pingSeconds}s), subscribing to {_chatChannel}");
                Connected?.Invoke(_channelSlug);
                await SendCommandAsync("subscribe", $"\"subscribe\":{{\"channel\":{JsonSerializer.Serialize(_chatChannel)}}}");
                return;

            case "subscribe":
                if (error is not null)
                {
                    DebugLog.Write($"KickChatClient: could not subscribe to the chat: {error}");
                    NotifyDisconnected(socket, "subscribe refused: " + error);
                    return;
                }
                DebugLog.Write("KickChatClient: subscription succeeded");
                return;

            case "refresh":
                if (error is not null)
                {
                    DebugLog.Write($"KickChatClient: Kick refused the new token: {error}");
                    NotifyDisconnected(socket, "token refused: " + error);
                    return;
                }
                ScheduleTokenRefresh(reply.TryGetProperty("refresh", out var refreshEl) ? refreshEl : default);
                DebugLog.Write("KickChatClient: token refreshed");
                return;
        }
    }

    private async Task HandlePushAsync(ClientWebSocket socket, JsonElement push)
    {
        if (push.TryGetProperty("disconnect", out var disconnect))
        {
            int code = disconnect.TryGetProperty("code", out var codeEl) && codeEl.TryGetInt32(out int c) ? c : 0;
            string reason = disconnect.TryGetProperty("reason", out var reasonEl) ? reasonEl.GetString() ?? "" : "";
            NotifyDisconnected(socket, $"Kick disconnected: {code} {reason}");
            return;
        }

        if (push.TryGetProperty("unsub", out _))
        {
            DebugLog.Write("KickChatClient: Kick unsubscribed us from the chat");
            NotifyDisconnected(socket, "unsubscribed");
            return;
        }

        // Kick's own envelope inside the push: {"event": "App\\Events\\ChatMessageEvent", "data": "<json>"}.
        if (
            push.TryGetProperty("pub", out var pub)
            && pub.TryGetProperty("data", out var envelope)
            && envelope.ValueKind == JsonValueKind.Object
        )
            await HandleFrameAsync(envelope.GetRawText());
    }

    /// <summary>The server tells how often it pings and how long the token lasts; ask for a new token well before it runs out.</summary>
    private void ScheduleTokenRefresh(JsonElement connectReply)
    {
        if (connectReply.ValueKind != JsonValueKind.Object)
            return;

        if (connectReply.TryGetProperty("ping", out var pingEl) && pingEl.TryGetInt32(out int ping) && ping > 0)
            _pingSeconds = ping;

        bool expires = connectReply.TryGetProperty("expires", out var expiresEl) && expiresEl.ValueKind == JsonValueKind.True;
        int ttl = connectReply.TryGetProperty("ttl", out var ttlEl) && ttlEl.TryGetInt32(out int t) ? t : 0;

        _refreshAsked = false;
        _refreshAtUtc = expires && ttl > 0
            ? DateTime.UtcNow.AddSeconds(Math.Max(ttl - 120, ttl / 2))
            : null;
    }

    private void StartTokenRefreshIfDue(ClientWebSocket socket)
    {
        if (_refreshAtUtc is not { } due || _refreshAsked || DateTime.UtcNow < due)
            return;

        _refreshAsked = true;
        _ = RefreshTokenAsync(socket);
    }

    private async Task RefreshTokenAsync(ClientWebSocket socket)
    {
        try
        {
            string token = await FetchRealtimeTokenAsync();
            if (!ReferenceEquals(_socket, socket))
                return;
            await SendCommandAsync("refresh", $"\"refresh\":{{\"token\":{JsonSerializer.Serialize(token)}}}");
        }
        catch (Exception ex)
        {
            // Nothing to do: Kick ends the connection when the old token runs out, and the reconnect gets a new one.
            DebugLog.WriteException("KickChatClient.RefreshTokenAsync", ex);
        }
    }

    private async Task HandleFrameAsync(string frame)
    {
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
                case "App\\Events\\ChatMessageEvent":
                case "App\\Events\\ChatMessageSentEvent":
                    HandleChatMessageEvent(doc.RootElement);
                    return;

                case "App\\Events\\MessageDeletedEvent":
                    HandleMessageDeletedEvent(doc.RootElement);
                    return;

                case "App\\Events\\UserBannedEvent":
                    HandleUserBannedEvent(doc.RootElement, banned: true);
                    return;

                case "App\\Events\\UserUnbannedEvent":
                    HandleUserBannedEvent(doc.RootElement, banned: false);
                    return;

                case "App\\Events\\ChatroomClearEvent":
                    DebugLog.Write("KickChatClient: chat cleared");
                    ChatCleared?.Invoke();
                    return;
            }
        }
    }

    /// <summary>
    /// Kick wraps each event's payload as a JSON string inside "data"; parses it, or returns null when it is missing
    /// or malformed (these moderation events are best-effort: a bad one is ignored, never fatal).
    /// </summary>
    private static JsonDocument? ParseEventData(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("data", out var dataElement))
                return null;

            var raw = dataElement.ValueKind == JsonValueKind.String ? dataElement.GetString() : dataElement.GetRawText();
            return string.IsNullOrEmpty(raw) ? null : JsonDocument.Parse(raw);
        }
        catch (JsonException ex)
        {
            DebugLog.WriteException("KickChatClient.ParseEventData", ex);
            return null;
        }
    }

    // MessageDeletedEvent: { "id": <event>, "message": { "id": <deleted message id> }, ... }
    private void HandleMessageDeletedEvent(JsonElement root)
    {
        using var data = ParseEventData(root);
        if (data is null)
            return;

        if (
            data.RootElement.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("id", out var idEl)
        )
        {
            var id = idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : idEl.GetRawText();
            if (!string.IsNullOrEmpty(id))
                MessageDeleted?.Invoke(id);
        }
    }

    // UserBannedEvent / UserUnbannedEvent: { "user": { "id": ... }, "permanent": bool, ... }
    // A ban with "permanent": false is a timeout.
    private void HandleUserBannedEvent(JsonElement root, bool banned)
    {
        using var data = ParseEventData(root);
        if (data is null)
            return;

        if (
            !data.RootElement.TryGetProperty("user", out var user)
            || user.ValueKind != JsonValueKind.Object
            || !user.TryGetProperty("id", out var idEl)
        )
            return;

        var userId = idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : idEl.GetRawText();
        if (string.IsNullOrEmpty(userId))
            return;

        if (!banned)
        {
            UserUnbanned?.Invoke(userId);
            return;
        }

        bool permanent =
            !data.RootElement.TryGetProperty("permanent", out var permanentEl)
            || permanentEl.ValueKind != JsonValueKind.False;

        string username = "";
        if (user.TryGetProperty("username", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            username = nameEl.GetString() ?? "";
        if (username.Length == 0 && user.TryGetProperty("slug", out var slugEl) && slugEl.ValueKind == JsonValueKind.String)
            username = slugEl.GetString() ?? "";

        DateTime? expiresAt = null;
        if (
            !permanent
            && data.RootElement.TryGetProperty("expires_at", out var expiresEl)
            && expiresEl.ValueKind == JsonValueKind.String
            && DateTime.TryParse(
                expiresEl.GetString(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var parsed
            )
        )
            expiresAt = parsed;

        UserBanned?.Invoke(new KickBanEvent(userId, username, permanent, expiresAt));
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

        // The message's own id (needed to delete it) and the sender's numeric id (needed to time out / ban them).
        var messageId = data.TryGetProperty("id", out var messageIdEl) && messageIdEl.ValueKind == JsonValueKind.String
            ? messageIdEl.GetString()
            : null;
        string? senderId = null;

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
            if (sender.TryGetProperty("id", out var senderIdEl))
                senderId = senderIdEl.ValueKind == JsonValueKind.String ? senderIdEl.GetString() : senderIdEl.GetRawText();

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
                MessageId = string.IsNullOrEmpty(messageId) ? null : messageId,
                UserId = string.IsNullOrEmpty(senderId) ? null : senderId,
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

        // _sendLock stays alive on purpose: the window keeps using this same client for the next channel after a
        // dispose, and a SemaphoreSlim used like this holds nothing that needs releasing.
    }
}