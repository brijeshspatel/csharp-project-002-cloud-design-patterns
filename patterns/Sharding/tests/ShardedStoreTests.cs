namespace Sharding.Tests;

/// <summary>
/// What sharding guarantees: that a keyed read touches **one shard however many
/// exist**, that the same key always routes to the same shard, and that a query
/// without the key pays a visit to every shard.
///
/// All three are asserted against <see cref="ShardedStore.ShardsTouched"/>. In
/// memory four dictionaries answer as fast as one, so the routing is only
/// visible as a count.
/// </summary>
public class ShardedStoreTests
{
    private static ShardedStore StoreWithCustomers(int count)
    {
        ShardedStore store = new(shardCount: 4);
        for (int i = 1; i <= count; i++)
        {
            store.Add(new CustomerRecord($"C-{i:000}", $"Customer {i}", i % 2 == 0 ? "north" : "south"));
        }

        return store;
    }

    [Fact]
    public void Routes_a_key_to_the_same_shard_every_time()
    {
        int first = ShardKey.For("C-017", shardCount: 4);

        for (int attempt = 0; attempt < 100; attempt++)
        {
            Assert.Equal(first, ShardKey.For("C-017", shardCount: 4));
        }
    }

    [Fact]
    public void Distributes_keys_across_the_shards()
    {
        ShardedStore store = StoreWithCustomers(40);

        for (int shard = 0; shard < store.ShardCount; shard++)
        {
            // Forty keys over four shards: a routing that starves a shard
            // defeats the point of having it.
            Assert.True(
                store.RecordsIn(shard) > 0,
                $"shard {shard} holds nothing; the routing is not spreading keys");
        }
    }

    [Fact]
    public void Reads_a_keyed_record_from_one_shard()
    {
        ShardedStore store = StoreWithCustomers(40);

        CustomerRecord? found = store.Get("C-017");

        Assert.Equal("Customer 17", found?.Name);

        // One shard touched out of four. This count is the entire benefit, and
        // it is what a keyed read that quietly fans out breaks.
        Assert.Equal(1, store.ShardsTouched);
    }

    [Fact]
    public void Fans_out_across_every_shard_without_the_key()
    {
        ShardedStore store = StoreWithCustomers(40);

        int northern = store.ScanAll().Count(record => record.Region == "north");

        Assert.Equal(20, northern);

        // No key, so no routing: every shard pays. The fan-out is the cost the
        // partition key was chosen to avoid, and it should be visible.
        Assert.Equal(4, store.ShardsTouched);
    }

    [Fact]
    public void Reports_nothing_for_a_key_it_does_not_hold()
    {
        ShardedStore store = StoreWithCustomers(40);

        Assert.Null(store.Get("C-999"));

        // Absence is still answered by one shard: the routed one.
        Assert.Equal(1, store.ShardsTouched);
    }
}
