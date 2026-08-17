namespace CacheAside;

/// <summary>Something in the catalogue.</summary>
/// <param name="Sku">What identifies it.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Price">What it costs.</param>
public readonly record struct Product(string Sku, string Name, decimal Price);

/// <summary>
/// The backing store — a database, in a real system.
///
/// **It counts its reads, and that counter is the whole demonstration.** In
/// memory a read costs nothing, so a cache that merely returns faster proves
/// nothing to a reader: they see a dictionary lookup and have to take the
/// benefit on trust. Counting turns the saving into a number.
/// </summary>
public sealed class ProductStore
{
    private readonly Dictionary<string, Product> products = [];

    /// <summary>How many reads have actually reached the store.</summary>
    public int Reads { get; private set; }

    /// <summary>Reads a product, and counts having done so.</summary>
    public Product? Get(string sku)
    {
        Reads++;
        return products.TryGetValue(sku, out Product product) ? product : null;
    }

    /// <summary>Writes a product. Not counted — the count is about reads.</summary>
    public void Update(Product product) => products[product.Sku] = product;
}

/// <summary>What time it is, so the cache's expiry is assertable.</summary>
public interface IClock
{
    /// <summary>The current instant, in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>A clock the caller moves by hand.</summary>
public sealed class ManualClock : IClock
{
    /// <summary>Starts the clock at <paramref name="start"/>.</summary>
    public ManualClock(DateTimeOffset start) => UtcNow = start;

    /// <inheritdoc/>
    public DateTimeOffset UtcNow { get; private set; }

    /// <summary>Moves the clock forward.</summary>
    public void Advance(TimeSpan duration) => UtcNow += duration;
}

/// <summary>The real clock. What production would use.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
