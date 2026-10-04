using TTNOverlay.Twitch;

namespace TTNOverlay.Services;

/// <summary>
/// The messages AutoMod is currently holding, oldest first. Small and bounded (held messages are rare and Twitch
/// drops them after a few hours), kept in memory only. Not thread-safe: use it from the UI thread.
/// </summary>
internal sealed class AutoModQueue
{
    /// <summary>When a raid floods the queue, the oldest entries are dropped; Twitch still holds them until they expire.</summary>
    public const int MaxItems = 50;

    private readonly List<AutoModHeldMessage> _items = new();

    public int Count => _items.Count;

    public AutoModHeldMessage this[int index] => _items[index];

    /// <summary>Adds a message; returns false when that message id is already queued.</summary>
    public bool Add(AutoModHeldMessage message)
    {
        if (Find(message.MessageId) is not null)
            return false;

        _items.Add(message);
        if (_items.Count > MaxItems)
            _items.RemoveAt(0);
        return true;
    }

    public AutoModHeldMessage? Find(string messageId)
    {
        for (int i = 0; i < _items.Count; i++)
            if (string.Equals(_items[i].MessageId, messageId, StringComparison.Ordinal))
                return _items[i];
        return null;
    }

    /// <summary>Removes a message by id; returns the removed entry, or null when it wasn't queued.</summary>
    public AutoModHeldMessage? Remove(string messageId)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (!string.Equals(_items[i].MessageId, messageId, StringComparison.Ordinal))
                continue;

            var removed = _items[i];
            _items.RemoveAt(i);
            return removed;
        }
        return null;
    }

    public void Clear() => _items.Clear();
}
