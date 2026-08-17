using CacheAside;

// A product catalogue read far more often than it changes. The store counts its
// reads, because in memory the saving is otherwise invisible - the reader would
// see a dictionary lookup and have to take the benefit on trust.

Console.WriteLine("Cache-Aside - counting the reads the cache prevented");
Console.WriteLine(new string('=', 58));
Console.WriteLine();

ManualClock clock = new(new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero));
ProductStore store = new();
store.Update(new Product("SKU-1", "Kettle", 24.99m));

const int Reads = 10;

Console.WriteLine($"Without a cache: {Reads} reads");
Console.WriteLine(new string('-', 58));
for (int i = 0; i < Reads; i++)
{
    store.Get("SKU-1");
}

Console.WriteLine($"  the store was read {store.Reads} times");

Console.WriteLine();
Console.WriteLine($"With a cache: the same {Reads} reads");
Console.WriteLine(new string('-', 58));

ProductStore cached = new();
cached.Update(new Product("SKU-1", "Kettle", 24.99m));
CacheAsideRepository repository = new(cached, clock, TimeSpan.FromSeconds(60));

for (int i = 0; i < Reads; i++)
{
    repository.Get("SKU-1");
}

Console.WriteLine($"  the store was read {cached.Reads} time");
Console.WriteLine($"  {Reads - cached.Reads} reads never left the process");

Console.WriteLine();
Console.WriteLine("The cost: the cache does not know the store changed");
Console.WriteLine(new string('-', 58));

cached.Update(new Product("SKU-1", "Kettle", 19.99m));
Console.WriteLine($"  the store now says   {cached.Get("SKU-1")?.Price:0.00}");
Console.WriteLine($"  the cache still says {repository.Get("SKU-1")?.Price:0.00}  <- stale");

repository.Invalidate("SKU-1");
Console.WriteLine($"  after invalidation:  {repository.Get("SKU-1")?.Price:0.00}");

Console.WriteLine();
Console.WriteLine("Or wait for the entry to expire");
Console.WriteLine(new string('-', 58));

cached.Update(new Product("SKU-1", "Kettle", 14.99m));
Console.WriteLine($"  immediately after a price change: {repository.Get("SKU-1")?.Price:0.00}  <- stale");
clock.Advance(TimeSpan.FromSeconds(60));
Console.WriteLine($"  sixty seconds later:              {repository.Get("SKU-1")?.Price:0.00}");

Console.WriteLine();
Console.WriteLine("That staleness window is the trade, not a defect. See the README.");
