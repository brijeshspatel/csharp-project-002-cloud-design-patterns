namespace MaterializedView;

/// <summary>One order, as the source holds it.</summary>
/// <param name="OrderId">What identifies it.</param>
/// <param name="Region">Where it was placed.</param>
/// <param name="Amount">What it came to.</param>
public readonly record struct OrderRow(string OrderId, string Region, decimal Amount);

/// <summary>What the view holds for one region.</summary>
/// <param name="Region">Which region.</param>
/// <param name="Total">The sum of its orders.</param>
/// <param name="Orders">How many there were.</param>
public readonly record struct RegionTotal(string Region, decimal Total, int Orders);

/// <summary>
/// The source of truth: orders, one row each, shaped for writing rather than
/// for the question being asked.
///
/// **It counts the rows it hands out**, and that counter is the demonstration.
/// In memory an aggregation over a handful of rows is free, so a view that
/// merely answers faster proves nothing.
/// </summary>
public sealed class OrderSource
{
    private readonly List<OrderRow> orders = [];

    /// <summary>How many rows have been handed to a caller.</summary>
    public int RowsScanned { get; private set; }

    /// <summary>How many rows exist.</summary>
    public int Count => orders.Count;

    /// <summary>Adds an order.</summary>
    public void Add(OrderRow order) => orders.Add(order);

    /// <summary>Reads every row, counting each one.</summary>
    public IEnumerable<OrderRow> Scan()
    {
        foreach (OrderRow order in orders)
        {
            RowsScanned++;
            yield return order;
        }
    }
}

/// <summary>
/// A precomputed answer to a question the source is badly shaped for.
///
/// The source stores one row per order because that is what writing wants.
/// "What did the north region sell?" needs every one of those rows read and
/// summed — every time it is asked. The view does that work **once** and keeps
/// the answer.
///
/// **The cost is that the answer is only as current as the last rebuild.** The
/// view is not told when the source changes; it serves the old figure until
/// somebody rebuilds it. That window is the trade, and the tests assert it
/// rather than hiding it.
///
/// A materialised view is **derived data and always disposable.** Losing it
/// costs a rebuild, never data — which is why it can be rebuilt freely and why
/// it should never be the only copy of anything.
/// </summary>
public sealed class SalesByRegionView
{
    private readonly OrderSource source;
    private Dictionary<string, RegionTotal> totals = [];

    /// <summary>Creates a view over <paramref name="source"/>.</summary>
    public SalesByRegionView(OrderSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        this.source = source;
    }

    /// <summary>How many rebuilds have been done.</summary>
    public int Rebuilds { get; private set; }

    /// <summary>How many regions the view currently holds.</summary>
    public int Regions => totals.Count;

    /// <summary>
    /// Recomputes the view from the source as it stands now.
    ///
    /// A full rebuild rather than an incremental update, deliberately: it is
    /// simpler, it cannot drift from the source, and for a view of this size it
    /// is cheaper than tracking deltas. Incremental refresh is the right answer
    /// at a scale this model does not reach, and it brings a correctness burden
    /// with it.
    /// </summary>
    public void Rebuild()
    {
        Dictionary<string, RegionTotal> rebuilt = [];

        foreach (OrderRow order in source.Scan())
        {
            RegionTotal current = rebuilt.TryGetValue(order.Region, out RegionTotal existing)
                ? existing
                : new RegionTotal(order.Region, 0m, 0);

            rebuilt[order.Region] = current with
            {
                Total = current.Total + order.Amount,
                Orders = current.Orders + 1,
            };
        }

        totals = rebuilt;
        Rebuilds++;
    }

    /// <summary>Answers for one region, without touching the source.</summary>
    public RegionTotal? TotalFor(string region) =>
        totals.TryGetValue(region, out RegionTotal total) ? total : null;
}
