using Sharding;

// One logical store, four shards. The store counts the shards a read touches,
// because in memory the fan-out is otherwise invisible.

Console.WriteLine("Sharding - counting the shards a read touches");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

const int CustomerCount = 40;

ShardedStore store = new(shardCount: 4);
for (int i = 1; i <= CustomerCount; i++)
{
    store.Add(new CustomerRecord($"C-{i:000}", $"Customer {i}", i % 2 == 0 ? "north" : "south"));
}

Console.WriteLine($"{CustomerCount} customers routed across {store.ShardCount} shards:");
for (int shard = 0; shard < store.ShardCount; shard++)
{
    Console.WriteLine($"  shard {shard}: {store.RecordsIn(shard)} records");
}

Console.WriteLine();
Console.WriteLine("A keyed read: the key routes, one shard answers");
Console.WriteLine(new string('-', 60));

CustomerRecord? found = store.Get("C-017");
Console.WriteLine($"  C-017 -> {found?.Name}");
Console.WriteLine($"  shards touched: {store.ShardsTouched} of {store.ShardCount}");

Console.WriteLine();
Console.WriteLine("A query without the key: nothing routes, every shard pays");
Console.WriteLine(new string('-', 60));

int before = store.ShardsTouched;
int northern = store.ScanAll().Count(record => record.Region == "north");

Console.WriteLine($"  customers in the north region: {northern}");
Console.WriteLine($"  shards touched by that query:  {store.ShardsTouched - before} of {store.ShardCount}");

Console.WriteLine();
Console.WriteLine("The shard key decides which reads are cheap. Choose it for the");
Console.WriteLine("question the system actually asks; every other question fans out.");
