using System;
using System.Collections.Generic;
using System.Linq;

namespace TTNOverlay.Services;

/// <summary>
/// Synchronous LRU cache bounded by a configurable weight budget. Evicted and explicitly removed
/// values are passed to the onEvict callback so callers can dispose native/GPU resources (e.g.
/// Direct2D bitmaps/brushes). Not thread-safe. Intended for UI-thread-only caches, unlike the
/// async-aware <see cref="LruCache{TKey, TValue}"/>.
/// </summary>
public sealed class DisposingLruCache<TKey, TValue> where TKey : notnull
{
    private sealed class Entry
    {
        public required TValue Value;
        public required LinkedListNode<TKey> Node;
        public int Weight;
    }

    private readonly int _capacity;
    private readonly Func<TValue, int> _weigher;
    private readonly Action<TKey, TValue> _onEvict;
    private readonly Dictionary<TKey, Entry> _entries = new();
    private readonly LinkedList<TKey> _order = new();
    private int _totalWeight;

    public int Count => _entries.Count;
    public int TotalWeight => _totalWeight;
    public IEnumerable<TKey> Keys => _entries.Keys;
    public IEnumerable<TValue> Values => _entries.Values.Select(e => e.Value);

    /// <param name="capacity">Total weight budget. Must be greater than zero.</param>
    /// <param name="weigher">Computes the weight of a value; defaults to 1 per entry (count-based capacity). May return 0.</param>
    /// <param name="onEvict">Called for every value removed from the cache, whether by explicit removal or by LRU eviction.</param>
    public DisposingLruCache(int capacity, Func<TValue, int>? weigher = null, Action<TKey, TValue>? onEvict = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "La capacidad debe ser mayor que cero.");
        _capacity = capacity;
        _weigher = weigher ?? (_ => 1);
        _onEvict = onEvict ?? ((_, _) => { });
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            value = entry.Value;
            return true;
        }
        value = default!;
        return false;
    }

    public bool ContainsKey(TKey key) => _entries.ContainsKey(key);

    /// <summary>
    /// Inserts or replaces a value, moving it to most-recently-used, then evicts the oldest
    /// entries until back within capacity. Replacing an existing key evicts (and disposes via
    /// onEvict) its old value first.
    /// </summary>
    public void Set(TKey key, TValue value)
    {
        if (_entries.TryGetValue(key, out var existing))
        {
            _totalWeight -= existing.Weight;
            _order.Remove(existing.Node);
            _onEvict(key, existing.Value);
        }

        var node = _order.AddLast(key);
        int weight = Math.Max(0, _weigher(value));
        _entries[key] = new Entry { Value = value, Node = node, Weight = weight };
        _totalWeight += weight;

        EvictIfNeeded();
    }

    /// <summary>Moves an existing key to most-recently-used without changing its value.</summary>
    public void Touch(TKey key)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            _order.Remove(entry.Node);
            _order.AddLast(entry.Node);
        }
    }

    public bool Remove(TKey key)
    {
        if (!_entries.TryGetValue(key, out var entry))
            return false;
        _order.Remove(entry.Node);
        _entries.Remove(key);
        _totalWeight -= entry.Weight;
        _onEvict(key, entry.Value);
        return true;
    }

    /// <summary>Removes and disposes (via onEvict) every entry whose key matches the predicate.</summary>
    public void RemoveWhere(Func<TKey, bool> predicate)
    {
        List<TKey>? toRemove = null;
        foreach (var key in _entries.Keys)
            if (predicate(key))
                (toRemove ??= new List<TKey>()).Add(key);

        if (toRemove is null)
            return;
        foreach (var key in toRemove)
            Remove(key);
    }

    public void Clear()
    {
        foreach (var (key, entry) in _entries)
            _onEvict(key, entry.Value);
        _entries.Clear();
        _order.Clear();
        _totalWeight = 0;
    }

    private void EvictIfNeeded()
    {
        while (_totalWeight > _capacity && _order.Count > 0)
        {
            var oldest = _order.First!.Value;
            _order.RemoveFirst();
            if (_entries.TryGetValue(oldest, out var entry))
            {
                _totalWeight -= entry.Weight;
                _entries.Remove(oldest);
                _onEvict(oldest, entry.Value);
            }
        }
    }
}