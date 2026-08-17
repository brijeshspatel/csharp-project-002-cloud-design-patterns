namespace CacheAside.Tests;

/// <summary>
/// What cache-aside guarantees: that a repeated read does **not** reach the
/// store, that an expired entry does, and that the price of both is a window in
/// which the answer is stale.
///
/// Every assertion is made against <see cref="ProductStore.Reads"/> rather than
/// against timing. In memory a store read costs nothing, so the saving is only
/// visible as a count — a cache that is not observed to prevent a read has
/// demonstrated nothing.
/// </summary>
public class CacheAsideRepositoryTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    private static (CacheAsideRepository Repository, ProductStore Store, ManualClock Clock) Build(
        int ttlSeconds = 60)
    {
        ProductStore store = new();
        store.Update(new Product("SKU-1", "Kettle", 24.99m));
        ManualClock clock = new(Start);
        return (new CacheAsideRepository(store, clock, TimeSpan.FromSeconds(ttlSeconds)), store, clock);
    }

    [Fact]
    public void Reads_through_to_the_store_on_a_miss()
    {
        (CacheAsideRepository repository, ProductStore store, _) = Build();

        Product? product = repository.Get("SKU-1");

        Assert.Equal("Kettle", product?.Name);
        Assert.Equal(1, store.Reads);
    }

    [Fact]
    public void Serves_a_second_read_without_touching_the_store()
    {
        (CacheAsideRepository repository, ProductStore store, _) = Build();

        repository.Get("SKU-1");
        repository.Get("SKU-1");
        repository.Get("SKU-1");

        // Three reads, one store hit. This count **is** the pattern's benefit;
        // nothing else here would distinguish a cache from a pass-through.
        Assert.Equal(1, store.Reads);
    }

    [Fact]
    public void Reads_the_store_again_once_the_entry_has_expired()
    {
        (CacheAsideRepository repository, ProductStore store, ManualClock clock) = Build(ttlSeconds: 60);

        repository.Get("SKU-1");
        clock.Advance(TimeSpan.FromSeconds(60));
        repository.Get("SKU-1");

        Assert.Equal(2, store.Reads);
    }

    [Fact]
    public void Serves_a_stale_value_until_the_entry_is_invalidated()
    {
        (CacheAsideRepository repository, ProductStore store, _) = Build();

        repository.Get("SKU-1");
        store.Update(new Product("SKU-1", "Kettle", 19.99m));

        // The cache does not know the store changed. Serving 24.99 here is the
        // pattern working as designed, and it is the cost the pattern carries.
        Assert.Equal(24.99m, repository.Get("SKU-1")?.Price);
    }

    [Fact]
    public void Reaches_the_store_again_after_invalidation()
    {
        (CacheAsideRepository repository, ProductStore store, _) = Build();

        repository.Get("SKU-1");
        store.Update(new Product("SKU-1", "Kettle", 19.99m));
        repository.Invalidate("SKU-1");

        Assert.Equal(19.99m, repository.Get("SKU-1")?.Price);
        Assert.Equal(2, store.Reads);
    }
}
