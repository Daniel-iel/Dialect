namespace Dialect.Core.Compilation;

using System.Collections.Concurrent;

/// <summary>
/// Eviction strategy for the compiled query cache.
/// </summary>
public enum CacheEvictionStrategy
{
    /// <summary>
    /// No eviction - allows work duplication when cache is full.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Evicts the oldest (first added) entry when cache is full.
    /// </summary>
    Oldest = 1,
    
    /// <summary>
    /// Evicts a random entry when cache is full.
    /// </summary>
    Random = 2
}

/// <summary>
/// Thread-safe cache for compiled SQL queries, keyed by query shape.
/// Supports TTL (Time-To-Live) and configurable eviction strategies.
/// </summary>
public sealed class CompiledQueryCache
{
    private readonly ConcurrentDictionary<QueryShapeKey, CacheEntry> _cache;
    private readonly int _maxEntries;
    private readonly CacheEvictionStrategy _evictionStrategy;
    private readonly TimeSpan? _ttl;
    private readonly Queue<QueryShapeKey> _accessOrder;
    private long _hits;
    private long _misses;
    private long _evictions;

    private class CacheEntry
    {
        public string Sql { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public CompiledQueryCache(int maxEntries = 1000, CacheEvictionStrategy strategy = CacheEvictionStrategy.None, TimeSpan? ttl = null)
    {
        if (maxEntries <= 0)
            throw new ArgumentException("Max entries must be greater than 0", nameof(maxEntries));
        
        _maxEntries = maxEntries;
        _evictionStrategy = strategy;
        _ttl = ttl;
        _cache = new ConcurrentDictionary<QueryShapeKey, CacheEntry>();
        _accessOrder = strategy == CacheEvictionStrategy.Oldest ? new Queue<QueryShapeKey>() : null;
    }

    /// <summary>
    /// Gets or adds a compiled SQL string to the cache.
    /// </summary>
    public string GetOrAdd(QueryShapeKey key, Func<QueryShapeKey, string> factory)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        // Check for expired entries and remove them
        CleanupExpiredEntries();

        // Try to get from cache
        if (_cache.TryGetValue(key, out var entry))
        {
            Interlocked.Increment(ref _hits);
            return entry.Sql;
        }

        Interlocked.Increment(ref _misses);
        var sql = factory(key);

        // Add to cache if there's space, or apply eviction strategy
        if (_cache.Count >= _maxEntries)
        {
            if (_evictionStrategy == CacheEvictionStrategy.None)
            {
                // Return without caching
                return sql;
            }
            else if (_evictionStrategy == CacheEvictionStrategy.Oldest)
            {
                // Remove oldest entry
                if (_accessOrder?.TryDequeue(out var oldestKey) == true)
                {
                    _cache.TryRemove(oldestKey, out _);
                    Interlocked.Increment(ref _evictions);
                }
            }
            else if (_evictionStrategy == CacheEvictionStrategy.Random)
            {
                // Remove random entry
                var randomKey = _cache.Keys.ElementAt(Random.Shared.Next(_cache.Count));
                _cache.TryRemove(randomKey, out _);
                Interlocked.Increment(ref _evictions);
            }
        }

        // Add the new entry
        var newEntry = new CacheEntry { Sql = sql, CreatedAt = DateTime.UtcNow };
        _cache.TryAdd(key, newEntry);
        
        if (_accessOrder != null)
        {
            _accessOrder.Enqueue(key);
        }

        return sql;
    }

    /// <summary>
    /// Cleans up expired entries based on TTL.
    /// </summary>
    private void CleanupExpiredEntries()
    {
        if (_ttl == null)
            return;

        var expiration = DateTime.UtcNow - _ttl.Value;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.CreatedAt < expiration)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
            Interlocked.Increment(ref _evictions);
        }
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _accessOrder?.Clear();
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
        Interlocked.Exchange(ref _evictions, 0);
    }

    /// <summary>
    /// Gets the current cache size.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public (long Hits, long Misses, long Evictions, double HitRate) GetStatistics()
    {
        var total = _hits + _misses;
        var hitRate = total > 0 ? (double)_hits / total : 0;
        return (_hits, _misses, _evictions, hitRate);
    }
}

/// <summary>
/// Represents the structural shape of a query (ignoring parameter values).
/// Used as a cache key to avoid re-rendering identical query structures.
/// </summary>
public sealed record QueryShapeKey(
    string QueryType, // "SELECT", "INSERT", "UPDATE", "DELETE", "ROUTINE"
    string StructureHash) // Hash of AST structure
{
    public override int GetHashCode()
    {
        unchecked
        {
            return (QueryType.GetHashCode() * 397) ^ StructureHash.GetHashCode();
        }
    }
};
