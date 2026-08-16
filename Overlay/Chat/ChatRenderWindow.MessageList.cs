using TTNOverlay.Models;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: message list bookkeeping, including timed expiry of old messages.
/// </summary>
internal sealed partial class ChatRenderWindow
{

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

        var cutoff = DateTime.Now.AddSeconds(-_settings.MessageTimeoutSeconds);
        int removed = _messages.RemoveAll(m => !m.IsPersistent && m.ReceivedAt <= cutoff);
        if (removed > 0)
            RequestRender();
    }

    private void AddMessage(ChatMessage msg)
    {
        _messages.Add(msg);
        while (_messages.Count > Math.Max(1, _settings.MaxMessages))
            _messages.RemoveAt(0);

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

        var ordered = msg.Emotes.OrderBy(e => e.Start).ToList();
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