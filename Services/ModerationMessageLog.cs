namespace TTNOverlay.Services;

/// <summary>What Twitch told us happened to a chatter (CLEARCHAT with a target user).</summary>
internal enum ModerationUserState : byte
{
    None,
    TimedOut,
    Banned,
}

/// <summary>
/// One chat message as the moderation tab needs it: only strings and flags, none of the render-side weight of a
/// ChatMessage (badge/emote lists, bitmaps), so keeping a couple of thousand of them costs about a megabyte at worst.
/// </summary>
internal sealed class ModerationLogEntry
{
    public ModerationLogEntry(
        string messageId,
        string userId,
        string login,
        string displayName,
        string text,
        DateTime receivedAtUtc,
        bool isProtected
    )
    {
        MessageId = messageId;
        UserId = userId;
        Login = login;
        DisplayName = displayName;
        Text = text;
        ReceivedAtUtc = receivedAtUtc;
        IsProtected = isProtected;
    }

    public string MessageId { get; }
    public string UserId { get; }
    public string Login { get; }
    public string DisplayName { get; }
    public string Text { get; }

    /// <summary>Arrival time. Kept non-decreasing across the log by <see cref="ModerationMessageLog.Add"/>.</summary>
    public DateTime ReceivedAtUtc { get; internal set; }

    /// <summary>
    /// Broadcaster / moderator / staff (or the logged-in moderator themself): Twitch rejects deleting, muting or
    /// banning these, so the UI doesn't offer actions on them.
    /// </summary>
    public bool IsProtected { get; }

    public bool IsDeleted { get; internal set; }
    public ModerationUserState UserState { get; internal set; }

    // ---- Render cache, owned by the moderation tab's renderer ----

    /// <summary>Row height measured for <see cref="CachedWidth"/> under layout version <see cref="CachedVersion"/>.</summary>
    public float CachedHeight;

    public float CachedWidth;
    public int CachedVersion;

    /// <summary>Local "HH:mm:ss", formatted once on first draw instead of on every frame.</summary>
    public string? TimeText;

    /// <summary>Forces the row to be measured again (its text changed, or fonts/language changed).</summary>
    internal void InvalidateLayout() => CachedWidth = 0f;
}

/// <summary>
/// Bounded, time-ordered log of recent chat messages for the moderation tab. A ring buffer: adding and evicting are
/// O(1), the "last N minutes" window is found with a binary search, and nothing is allocated per message beyond the
/// entry itself. Not thread-safe: use it from the UI thread only (the same thread that feeds the chat list).
/// </summary>
internal sealed class ModerationMessageLog
{
    public const int DefaultCapacity = 2000;
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromMinutes(60);

    private readonly int _capacity;
    private readonly TimeSpan _retention;
    private ModerationLogEntry?[] _items;
    private int _head;
    private int _count;

    // Ids of users whose rows carry a muted/banned tag. Lets the per-message hot path ask "is this user flagged?"
    // in O(1) and only scan the log when the answer is yes.
    private readonly HashSet<string> _flaggedUserIds = new(StringComparer.Ordinal);

    public ModerationMessageLog(int capacity = DefaultCapacity, TimeSpan? retention = null)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;
        _retention = retention ?? DefaultRetention;
        _items = new ModerationLogEntry?[Math.Min(64, capacity)];
    }

    public int Count => _count;

    /// <summary>Entry by logical index: 0 is the oldest, Count - 1 the newest.</summary>
    public ModerationLogEntry this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _items[(_head + index) % _items.Length]!;
        }
    }

    public void Add(ModerationLogEntry entry)
    {
        // Keep timestamps non-decreasing even if the system clock steps back, so the binary search stays valid.
        if (_count > 0)
        {
            var newest = this[_count - 1].ReceivedAtUtc;
            if (entry.ReceivedAtUtc < newest)
                entry.ReceivedAtUtc = newest;
        }

        var cutoff = entry.ReceivedAtUtc - _retention;
        while (_count > 0 && this[0].ReceivedAtUtc < cutoff)
            DropOldest();

        if (_count == _capacity)
            DropOldest();

        if (_count == _items.Length)
            Grow();

        _items[(_head + _count) % _items.Length] = entry;
        _count++;
    }

    /// <summary>
    /// Logical index of the first entry received at or after <paramref name="cutoffUtc"/>, or Count if there is
    /// none. Everything from that index to Count - 1 is the requested time window.
    /// </summary>
    public int FirstIndexAtOrAfter(DateTime cutoffUtc)
    {
        int lo = 0;
        int hi = _count;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (this[mid].ReceivedAtUtc < cutoffUtc)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    /// <summary>CLEARMSG: marks one message as deleted. Deleted messages are almost always recent, so scan from the newest.</summary>
    public bool MarkMessageDeleted(string messageId)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            var entry = this[i];
            if (string.Equals(entry.MessageId, messageId, StringComparison.Ordinal))
            {
                entry.IsDeleted = true;
                entry.InvalidateLayout();
                return true;
            }
        }
        return false;
    }

    /// <summary>CLEARCHAT with a target user: Twitch purges that user's messages (timeout or ban).</summary>
    public int MarkUserState(string userId, ModerationUserState state)
    {
        int changed = 0;
        for (int i = 0; i < _count; i++)
        {
            var entry = this[i];
            if (string.Equals(entry.UserId, userId, StringComparison.Ordinal))
            {
                entry.UserState = state;
                entry.InvalidateLayout();
                changed++;
            }
        }

        if (changed > 0)
        {
            if (state == ModerationUserState.None)
                _flaggedUserIds.Remove(userId);
            else
                _flaggedUserIds.Add(userId);
        }
        return changed;
    }

    /// <summary>
    /// Removes the muted/banned tag from a user's rows. Free when the user isn't flagged, so it is safe to call for
    /// every incoming message: someone who can write again is no longer banned or timed out.
    /// </summary>
    public int ClearUserState(string userId) =>
        _flaggedUserIds.Contains(userId) ? MarkUserState(userId, ModerationUserState.None) : 0;

    /// <summary>Same as <see cref="ClearUserState"/> but by login, for unbans started from the panel's banned list.</summary>
    public int ClearUserStateByLogin(string login)
    {
        if (_flaggedUserIds.Count == 0)
            return 0;

        string? userId = null;
        for (int i = _count - 1; i >= 0; i--)
        {
            var entry = this[i];
            if (entry.UserState != ModerationUserState.None && string.Equals(entry.Login, login, StringComparison.OrdinalIgnoreCase))
            {
                userId = entry.UserId;
                break;
            }
        }
        return userId is null ? 0 : MarkUserState(userId, ModerationUserState.None);
    }

    /// <summary>
    /// Drops the tag from every flagged user who is no longer in <paramref name="stillRestrictedUserIds"/> (the
    /// current banned/timed-out list). Catches unbans and expired timeouts done outside the app.
    /// </summary>
    public int ReconcileUserStates(HashSet<string> stillRestrictedUserIds)
    {
        if (_flaggedUserIds.Count == 0)
            return 0;

        List<string>? stale = null;
        foreach (var id in _flaggedUserIds)
            if (!stillRestrictedUserIds.Contains(id))
                (stale ??= new List<string>()).Add(id);

        int changed = 0;
        if (stale is not null)
            foreach (var id in stale)
                changed += MarkUserState(id, ModerationUserState.None);

        // Ids with no rows left in the log (evicted) can't be tagged anymore.
        _flaggedUserIds.IntersectWith(stillRestrictedUserIds);
        return changed;
    }

    /// <summary>CLEARCHAT without a target: the whole chat was cleared.</summary>
    public void MarkAllDeleted()
    {
        for (int i = 0; i < _count; i++)
        {
            var entry = this[i];
            entry.IsDeleted = true;
            entry.InvalidateLayout();
        }
    }

    /// <summary>
    /// Collects the ids of one user's messages that can still be deleted (not already deleted) from
    /// <paramref name="cutoffUtc"/> on, oldest first.
    /// </summary>
    public int CollectDeletableMessageIds(string userId, DateTime cutoffUtc, List<string> into)
    {
        int added = 0;
        for (int i = FirstIndexAtOrAfter(cutoffUtc); i < _count; i++)
        {
            var entry = this[i];
            if (entry.IsDeleted || !string.Equals(entry.UserId, userId, StringComparison.Ordinal))
                continue;
            into.Add(entry.MessageId);
            added++;
        }
        return added;
    }

    public void Clear()
    {
        Array.Clear(_items);
        _head = 0;
        _count = 0;
        _flaggedUserIds.Clear();
    }

    private void DropOldest()
    {
        _items[_head] = null;
        _head = (_head + 1) % _items.Length;
        _count--;
        if (_count == 0)
            _head = 0;
    }

    private void Grow()
    {
        int newLength = Math.Min(_items.Length * 2, _capacity);
        var grown = new ModerationLogEntry?[newLength];
        for (int i = 0; i < _count; i++)
            grown[i] = _items[(_head + i) % _items.Length];
        _items = grown;
        _head = 0;
    }
}
