namespace Sharding;

/// <summary>One customer, as a shard holds it.</summary>
/// <param name="Id">The shard key. Everything routes on this.</param>
/// <param name="Name">Who they are.</param>
/// <param name="Region">A field queries want that nothing routes on.</param>
public readonly record struct CustomerRecord(string Id, string Name, string Region);

/// <summary>
/// Routes a key to a shard, deterministically.
///
/// **The hash must be stable across processes and versions**, which is why this
/// is FNV-1a rather than <see cref="string.GetHashCode()"/> — .NET randomises
/// that per process, and a routing that moves between runs strands every record
/// where the previous run put it.
/// </summary>
public static class ShardKey
{
    /// <summary>Which of <paramref name="shardCount"/> shards owns <paramref name="key"/>.</summary>
    public static int For(string key, int shardCount)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);

        // FNV-1a, 32-bit: small, stable and well spread for short keys.
        uint hash = 2166136261;
        foreach (char c in key)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return (int)(hash % (uint)shardCount);
    }
}

/// <summary>
/// One logical store over several physical shards, each holding the records
/// whose keys route to it.
///
/// **It counts the shards a read touches**, and that counter is the
/// demonstration. In memory four dictionaries answer as fast as one, so a
/// keyed read that quietly fans out costs nothing here — and everything at
/// scale, where every touched shard is a network call and the slowest one sets
/// the latency.
/// </summary>
public sealed class ShardedStore
{
    private readonly Dictionary<string, CustomerRecord>[] shards;

    /// <summary>Creates a store over <paramref name="shardCount"/> shards.</summary>
    public ShardedStore(int shardCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);

        shards = new Dictionary<string, CustomerRecord>[shardCount];
        for (int i = 0; i < shards.Length; i++)
        {
            shards[i] = [];
        }
    }

    /// <summary>How many shards there are.</summary>
    public int ShardCount => shards.Length;

    /// <summary>How many shards reads have touched.</summary>
    public int ShardsTouched { get; private set; }

    /// <summary>How many records shard <paramref name="shard"/> holds.</summary>
    public int RecordsIn(int shard) => shards[shard].Count;

    /// <summary>Adds a record to the shard its key routes to.</summary>
    public void Add(CustomerRecord record) =>
        shards[ShardKey.For(record.Id, shards.Length)][record.Id] = record;

    /// <summary>A keyed read: routes, then touches exactly that shard.</summary>
    public CustomerRecord? Get(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        Dictionary<string, CustomerRecord> shard = shards[ShardKey.For(id, shards.Length)];
        ShardsTouched++;

        return shard.TryGetValue(id, out CustomerRecord record) ? record : null;
    }

    /// <summary>
    /// A read without the key: nothing routes, so every shard is touched. This
    /// is the fan-out the shard key was chosen to avoid, made visible.
    /// </summary>
    public IEnumerable<CustomerRecord> ScanAll()
    {
        foreach (Dictionary<string, CustomerRecord> shard in shards)
        {
            ShardsTouched++;
            foreach (CustomerRecord record in shard.Values)
            {
                yield return record;
            }
        }
    }
}
