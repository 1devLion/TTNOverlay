using System.Net.WebSockets;
using System.Text;
using TTNOverlay.Models;
using TTNOverlay.Services;
using TTNOverlay.Streamlabs;

namespace TTNOverlay.Twitch;

/// <summary>
/// WebSocket-based Twitch IRC client: connects to chat, parses incoming messages/tags, and raises them as ChatMessages.
/// </summary>
public class TwitchIrcClient : ITwitchIrcClient
{
    private const string WsUrl = "wss://irc-ws.chat.twitch.tv:443";
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private string _channel = "";

    public event Action<ChatMessage>? MessageReceived;
    public event Action<string>? Connected;
    public event Action<string>? Disconnected;
    public event Action<Exception>? Error;

    public async Task ConnectAsync(string channel)
    {
        _channel = channel.ToLowerInvariant().TrimStart('#');
        DebugLog.Write($"Connecting to channel '{_channel}'...");

        _cts = new CancellationTokenSource();
        _socket = new ClientWebSocket();

        await _socket.ConnectAsync(new Uri(WsUrl), _cts.Token);
        DebugLog.Write($"WebSocket connected, status: {_socket.State}");

        var anonUser = $"justinfan{Random.Shared.Next(10000, 99999)}";
        await SendAsync("CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership");
        await SendAsync($"NICK {anonUser}");
        await SendAsync($"JOIN #{_channel}");
        DebugLog.Write($"NICK={anonUser}, JOIN #{_channel} sent");

        Connected?.Invoke(_channel);
        _ = ReceiveLoopAsync(_cts.Token);
    }

    private async Task SendAsync(string message)
    {
        if (_socket is null)
            return;
        var bytes = Encoding.UTF8.GetBytes(message + "\r\n");
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        try
        {
            while (_socket is { State: WebSocketState.Open } && !token.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(buffer, token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    DebugLog.Write("Socket closed by the server");
                    Disconnected?.Invoke("closed by server");
                    break;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage)
                    continue;

                var chunk = sb.ToString();
                sb.Clear();

                foreach (var line in chunk.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                {
                    await HandleLineAsync(line);
                }
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            DebugLog.WriteException("ReceiveLoopAsync", ex);
            Error?.Invoke(ex);
        }
    }

    private async Task HandleLineAsync(string line)
    {
        DebugLog.Write($"RECV: {line}");

        if (line.StartsWith("PING"))
        {
            await SendAsync("PONG :tmi.twitch.tv");
            return;
        }

        var parsed = IrcMessageParser.Parse(line);

        switch (parsed.Command)
        {
            case "PRIVMSG":
                HandlePrivMsg(parsed);
                break;
            case "USERNOTICE":
                HandleUserNotice(parsed);
                break;
            case "NOTICE":
                if (parsed.Trailing is not null)
                {
                    MessageReceived?.Invoke(
                        new ChatMessage
                        {
                            IsSystem = true,
                            Text = parsed.Trailing,
                            Color = ChatColors.SystemGray,
                        }
                    );
                }
                break;
        }
    }

    private void HandlePrivMsg(ParsedIrcLine parsed)
    {
        var text = parsed.Trailing ?? "";
        bool isAction = false;
        if (text.StartsWith("\u0001ACTION ") && text.EndsWith("\u0001"))
        {
            isAction = true;
            text = text[8..^1];
        }

        var msg = new ChatMessage
        {
            Username = GetUsernameFromPrefix(parsed.Prefix),
            DisplayName = parsed.Tags.GetValueOrDefault("display-name", ""),
            Color = string.IsNullOrEmpty(parsed.Tags.GetValueOrDefault("color"))
                ? ChatColors.DefaultUserGray
                : parsed.Tags["color"],
            Text = text,
            IsAction = isAction,
            Badges = ParseBadges(parsed.Tags.GetValueOrDefault("badges", "")),
            Emotes = ParseEmotes(parsed.Tags.GetValueOrDefault("emotes", "")),
        };

        if (string.IsNullOrEmpty(msg.DisplayName))
            msg.DisplayName = msg.Username;

        MessageReceived?.Invoke(msg);
    }

    private void HandleUserNotice(ParsedIrcLine parsed)
    {
        var msgId = parsed.Tags.GetValueOrDefault("msg-id");
        var systemMsg = parsed.Tags.GetValueOrDefault("system-msg", "");
        var displayName = parsed.Tags.GetValueOrDefault("display-name", "");

        string text;
        string? announcementColor = null;
        var emotes = new List<EmotePosition>();
        (string? PlanName, int? StreakMonths, string? ImageUrl)? subInfo = null;

        if (msgId == "announcement")
        {

            announcementColor = parsed.Tags.GetValueOrDefault("msg-param-color");
            text = parsed.Trailing ?? "";
            emotes = ParseEmotes(parsed.Tags.GetValueOrDefault("emotes", ""));
            DebugLog.Write(
                $"Announcement (USERNOTICE) detected, color='{announcementColor}', text='{text}', emotes='{parsed.Tags.GetValueOrDefault("emotes")}'"
            );
        }
        else
        {
            subInfo = ResolveSubEventInfo(msgId ?? "", parsed.Tags);

            text =
                LocalizationService.Instance.CurrentLanguage == AppLanguage.Spanish
                    ? EventTextLocalizer.Build(msgId ?? "", parsed.Tags, displayName, parsed.Trailing)
                        ?? systemMsg
                    : systemMsg;

            if (text == systemMsg && !string.IsNullOrEmpty(parsed.Trailing))
                text += $"\n\"{parsed.Trailing}\"";
        }

        var (platform, eventKind) = EventTypeIds.Classify(msgId);

        MessageReceived?.Invoke(
            new ChatMessage
            {
                DisplayName = displayName,
                IsSystem = true,
                Text = text,
                Color = ChatColors.TwitchAnnouncement,
                EventType = msgId,
                Platform = platform,
                EventKind = eventKind,
                AnnouncementColor = announcementColor,
                Emotes = emotes,
                SubPlanName = subInfo?.PlanName,
                StreakMonths = subInfo?.StreakMonths,
                EventImageUrl = subInfo?.ImageUrl,
            }
        );
    }

    private static (string? PlanName, int? StreakMonths, string? ImageUrl) ResolveSubEventInfo(
        string msgId,
        Dictionary<string, string> tags
    )
    {
        var subPlanRaw = tags.GetValueOrDefault("msg-param-sub-plan");

        var normalizedSubPlan = subPlanRaw == "Prime" ? "prime" : subPlanRaw;

        var planName = normalizedSubPlan is null
            ? null
            : SubEventVariationResolver.GetSubPlanName(normalizedSubPlan);

        int? streakMonths = null;
        if (msgId == "resub")
        {
            var shareStreak = tags.GetValueOrDefault("msg-param-should-share-streak") == "1";
            if (
                shareStreak
                && int.TryParse(tags.GetValueOrDefault("msg-param-streak-months"), out var streak)
            )
                streakMonths = streak;
        }

        var eventKind = EventTypeIds.ParseTwitchMsgId(msgId);

        (string? Format, string? ImageUrl)? variation = eventKind switch
        {
            EventType.Sub => SubEventVariationResolver.ResolveTierSubVariation(normalizedSubPlan),
            EventType.Resub => SubEventVariationResolver.ResolveTierSubVariation(normalizedSubPlan),
            EventType.SubGift => SubEventVariationResolver.ResolveCachedSubVariation(
                "subgift",
                false,
                normalizedSubPlan
            ),
            EventType.AnonSubGift => SubEventVariationResolver.ResolveCachedSubVariation(
                "subgift",
                true,
                normalizedSubPlan
            ),
            EventType.MysteryGiftSub => SubEventVariationResolver.ResolveCachedSubVariation(
                "submysterygift",
                false,
                normalizedSubPlan
            ),
            EventType.AnonMysteryGiftSub => SubEventVariationResolver.ResolveCachedSubVariation(
                "submysterygift",
                true,
                normalizedSubPlan
            ),
            _ => null,
        };

        var imageUrl = ImageUrlHelper.Normalize(variation?.ImageUrl);

        return (planName, streakMonths, imageUrl);
    }

    private static string GetUsernameFromPrefix(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return "";
        var bang = prefix.IndexOf('!');
        return bang > 0 ? prefix[..bang] : prefix;
    }

    private static List<Badge> ParseBadges(string raw)
    {
        var list = new List<Badge>();
        if (string.IsNullOrEmpty(raw))
            return list;
        foreach (var entry in raw.Split(','))
        {
            var parts = entry.Split('/');
            if (parts.Length == 2)
                list.Add(new Badge { Name = parts[0], Version = parts[1] });
        }
        return list;
    }

    private static List<EmotePosition> ParseEmotes(string raw)
    {
        var list = new List<EmotePosition>();
        if (string.IsNullOrEmpty(raw))
            return list;

        foreach (var emoteEntry in raw.Split('/'))
        {
            var idx = emoteEntry.IndexOf(':');
            if (idx < 0)
                continue;
            var id = emoteEntry[..idx];
            var ranges = emoteEntry[(idx + 1)..].Split(',');
            foreach (var range in ranges)
            {
                var bounds = range.Split('-');
                if (
                    bounds.Length == 2
                    && int.TryParse(bounds[0], out var start)
                    && int.TryParse(bounds[1], out var end)
                )
                {
                    list.Add(
                        new EmotePosition
                        {
                            Id = id,
                            Start = start,
                            End = end,
                        }
                    );
                }
            }
        }
        // Twitch's `emotes` tag is grouped by emote ID, not ordered by position in the text.
        // Sort once here so downstream consumers (rendering) can assume position order and
        // don't need to re-sort on every draw/measure pass.
        list.Sort((a, b) => a.Start.CompareTo(b.Start));
        return list;
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

            }
        }
        _socket?.Dispose();
    }
}