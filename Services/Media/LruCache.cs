namespace TTNOverlay.Services;

/// <summary>
/// Generic async-aware LRU cache with a configurable per-entry weight and a total memory budget.
/// </summary>
public class LruCache<TKey, TValue> where TKey : notnull
{
    private class Entry
    {
        public required Task<TValue> Task;
        public required LinkedListNode<TKey> Node;
        public int Weight = 1;
    }

    private readonly int _capacity;
    private readonly Func<TValue, int>? _weigher;
    private readonly Dictionary<TKey, Entry> _cache = new();
    private readonly LinkedList<TKey> _order = new();
    private int _totalWeight;

    public LruCache(int capacity, Func<TValue, int>? weigher = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        _capacity = capacity;
        _weigher = weigher;
    }

    public Task<TValue> GetOrAdd(TKey key, Func<TKey, Task<TValue>> factory)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                _order.Remove(existing.Node);
                _order.AddLast(existing.Node);
                return existing.Task;
            }

            var node = _order.AddLast(key);
            var entry = new Entry { Task = null!, Node = node };
            var task = factory(key);
            entry.Task = task;
            _cache[key] = entry;
            _totalWeight += entry.Weight;

            EvictIfNeeded();

            _ = task.ContinueWith(t =>
            {
                lock (_cache)
                {
                    if (!_cache.TryGetValue(key, out var current) || !ReferenceEquals(current, entry))
                        return;

                    if (!t.IsCompletedSuccessfully)
                    {
                        // Don't let a transient failure (e.g. a network timeout downloading an
                        // animated emote) stay cached forever. Remove it so the next GetOrAdd
                        // triggers a fresh factory() call instead of returning the same failed
                        // Task until LRU pressure eventually evicts it.
                        _order.Remove(current.Node);
                        _totalWeight -= current.Weight;
                        _cache.Remove(key);
                        return;
                    }

                    if (_weigher is not null)
                    {
                        _totalWeight -= entry.Weight;
                        entry.Weight = Math.Max(1, _weigher(t.Result));
                        _totalWeight += entry.Weight;

                        EvictIfNeeded();
                    }
                }
            }, TaskScheduler.Default);

            return task;
        }
    }

    private void EvictIfNeeded()
    {
        while (_totalWeight > _capacity && _order.Count > 0)
        {
            var oldest = _order.First!.Value;
            _order.RemoveFirst();
            if (_cache.TryGetValue(oldest, out var entry))
            {
                _totalWeight -= entry.Weight;
                _cache.Remove(oldest);
            }
        }
    }

    public void Clear()
    {
        lock (_cache)
        {
            _cache.Clear();
            _order.Clear();
            _totalWeight = 0;
        }
    }
}