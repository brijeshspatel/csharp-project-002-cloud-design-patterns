namespace CacheAside;

/// <summary>
/// Reads through a cache, filling it on a miss.
///
/// The name is the design: the cache sits **aside** the store rather than in
/// front of it. The application, not the cache, knows how to load a value — so
/// a cache failure degrades to a slow read rather than an outage, and a store
/// that is written by anything other than this repository is still correct,
/// eventually.
///
/// **Expiry is the only thing keeping the cache honest.** Nothing tells this
/// repository that the store changed; the entry simply stops being trusted after
/// its time-to-live. That window — where the cache serves a value the store no
/// longer holds — is not a defect. It is the trade the pattern makes, and the
/// tests assert it deliberately.
/// </summary>
public sealed class CacheAsideRepository
{
    private readonly ProductStore store;
    private readonly IClock clock;
    private readonly TimeSpan timeToLive;
    private readonly Dictionary<string, Entry> cache = [];

    /// <summary>Creates a repository over <paramref name="store"/>.</summary>
    /// <param name="store">Where values really live.</param>
    /// <param name="clock">Where the current time comes from.</param>
    /// <param name="timeToLive">How long a cached value is trusted.</param>
    public CacheAsideRepository(ProductStore store, IClock clock, TimeSpan timeToLive)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeToLive, TimeSpan.Zero);

        this.store = store;
        this.clock = clock;
        this.timeToLive = timeToLive;
    }

    /// <summary>How many entries are currently cached.</summary>
    public int CachedEntries => cache.Count;

    /// <summary>Reads a product, from the cache where possible.</summary>
    public Product? Get(string sku)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        if (cache.TryGetValue(sku, out Entry entry) && clock.UtcNow < entry.ExpiresAt)
        {
            return entry.Product;
        }

        Product? loaded = store.Get(sku);

        // A miss on a value that does not exist is not cached. Caching absence
        // is a legitimate choice — it stops a hot missing key hammering the
        // store — but it is a different decision, and conflating the two is how
        // a cache starts serving "not found" for a record that now exists.
        if (loaded is not null)
        {
            cache[sku] = new Entry(loaded.Value, clock.UtcNow + timeToLive);
        }

        return loaded;
    }

    /// <summary>
    /// Drops a cached entry, so the next read reaches the store.
    ///
    /// Invalidating on write is what narrows the staleness window from the
    /// time-to-live to almost nothing — but only for writes that go through
    /// code which remembers to call this.
    /// </summary>
    public void Invalidate(string sku) => cache.Remove(sku);

    private readonly record struct Entry(Product Product, DateTimeOffset ExpiresAt);
}
