using TTNOverlay.Models;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: message list bookkeeping, including timed expiry of old messages.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    /// <summary>
    /// Backs _messages. Wraps a List&lt;ChatMessage&gt; with a lazily-advanced head index instead
    /// of calling List&lt;T&gt;.RemoveAt(0) on every trim. RemoveAt(0) is O(n) — it shifts every
    /// remaining element down one slot — so calling it once per incoming chat message forever, at
    /// steady state once MaxMessages is reached, makes sustained throughput O(n) per message in
    /// high-traffic channels with a high MaxMessages setting. RemoveOldest() instead just advances
    /// _head (O(1)); the backing list is only physically compacted (a single List.RemoveRange)
    /// once the dead space in front of _head grows large enough, amortizing that cost across many
    /// trims instead of paying it on every single one. Implements IReadOnlyList&lt;ChatMessage&gt;
    /// so it's a drop-in replacement everywhere _messages is read (Count, indexer, foreach), and
    /// so it shares a common type with _dashboardEvents (still a plain List&lt;ChatMessage&gt;,
    /// small and fixed-size — not worth this) wherever both are used interchangeably.
    /// </summary>
    private sealed class MessageBuffer : IReadOnlyList<ChatMessage>
    {
        private const int MinCompactionSlack = 64;

        private readonly List<ChatMessage> _items = new();
        private int _head;

        public int Count => _items.Count - _head;

        public ChatMessage this[int index] => _items[_head + index];

        public void Add(ChatMessage msg) => _items.Add(msg);

        /// <summary>Removes and returns the oldest (index 0) message.</summary>
        public ChatMessage RemoveOldest()
        {
            var removed = _items[_head];
            _head++;
            if (_head >= MinCompactionSlack && _head * 2 >= _items.Count)
                Compact();
            return removed;
        }

        public int RemoveAll(Predicate<ChatMessage> match)
        {
            Compact();
            return _items.RemoveAll(match);
        }

        public void Clear()
        {
            _items.Clear();
            _head = 0;
        }

        private void Compact()
        {
            if (_head == 0)
                return;
            _items.RemoveRange(0, _head);
            _head = 0;
        }

        public IEnumerator<ChatMessage> GetEnumerator()
        {
            for (int i = _head; i < _items.Count; i++)
                yield return _items[i];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private void EnsureExpirySweepTimerRunning()
    {
        if (_expirySweepTimer is not null || _settings.MessageTimeoutSeconds <= 0)
            return;

        _expirySweepTimer = new System.Threading.Timer(
            _ => PostToUiThread(SweepExpiredMessages),
            null,
            1000,
            1000
        );
    }

    private void SweepExpiredMessages()
    {
        if (_messages.Count == 0 || _settings.MessageTimeoutSeconds <= 0)
            return;

        var cutoff = DateTime.UtcNow.AddSeconds(-_settings.MessageTimeoutSeconds);
        int removed = _messages.RemoveAll(m =>
        {
            if (m.IsPersistent || m.ReceivedAt > cutoff)
                return false;
            RemoveMessageCaches(m);
            return true;
        });
        if (removed > 0)
            RequestRender();
    }

    private void AddMessage(ChatMessage msg)
    {
        _messages.Add(msg);
        int max = Math.Max(1, _settings.MaxMessages);
        while (_messages.Count > max)
            RemoveMessageCaches(_messages.RemoveOldest());
    }

    private void SeedWelcomeGuide()
    {
        AddMessage(
            new ChatMessage
            {
                Color = ChatColors.SystemGray,
                Text = "Welcome to TTNOverlay! :D",
                IsSystem = true,
                IsPersistent = true,
            }
        );
        AddMessage(
            new ChatMessage
            {
                Color = ChatColors.SystemGray,
                Text = "Open Settings (gear icon, top-left) and enter your Twitch channel name to connect the chat.",
                IsSystem = true,
                IsPersistent = true,
            }
        );
        AddMessage(
            new ChatMessage
            {
                Color = ChatColors.SystemGray,
                Text = "Want viewer count, badges, or moderation panel? Log in with Twitch:",
                IsSystem = true,
                IsPersistent = true,
                IsTwitchLoginPrompt = true,
            }
        );
        AddMessage(
            new ChatMessage
            {
                Color = ChatColors.SystemGray,
                Text = "Hotkeys: Ctrl+Shift+F7 toggle borders, F8 events, F9 moderation.",
                IsSystem = true,
                IsPersistent = true,
            }
        );
    }

    private record Segment(string? Text, EmotePosition? Emote)
    {
        public bool IsEmote => Emote is not null;
    }

    private static IEnumerable<Segment> SplitMessageIntoSegments(ChatMessage msg)
    {
        if (msg.Emotes.Count == 0)
        {
            yield return new Segment(msg.Text, null);
            yield break;
        }

        // Emotes already arrive ordered by position: TwitchIrcClient.ParseEmotes sorts once at
        // parse time, and KickChatClient.ParseKickEmotes produces them in order by construction.
        var ordered = msg.Emotes;
        int cursor = 0;
        var chars = msg.Text;

        foreach (var emote in ordered)
        {
            if (emote.Start > cursor && emote.Start <= chars.Length)
                yield return new Segment(chars[cursor..emote.Start], null);

            yield return new Segment(null, emote);
            cursor = Math.Min(emote.End + 1, chars.Length);
        }

        if (cursor < chars.Length)
            yield return new Segment(chars[cursor..], null);
    }
}