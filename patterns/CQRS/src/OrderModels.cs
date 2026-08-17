namespace Cqrs;

/// <summary>A command: place this order. Named for intent, not for data shape.</summary>
/// <param name="OrderId">Which order.</param>
/// <param name="CustomerId">Whose it is.</param>
/// <param name="Amount">What it comes to.</param>
public readonly record struct PlaceOrder(string OrderId, string CustomerId, decimal Amount);

/// <summary>What the read model serves: one customer's orders, pre-aggregated.</summary>
/// <param name="CustomerId">Whose summary.</param>
/// <param name="Orders">How many orders they have placed.</param>
/// <param name="Total">What those orders come to.</param>
public readonly record struct OrderSummary(string CustomerId, int Orders, decimal Total);

/// <summary>
/// The command side: current state, shaped for deciding whether a command may
/// proceed.
///
/// **This is a mutable store, not an event log.** CQRS separates reading from
/// writing and says nothing about keeping history — that is Event Sourcing,
/// a different pattern that this one is often paired with and does not require.
///
/// It refuses dashboard questions by throwing, deliberately. The erosion of
/// CQRS is always the same: one convenient read path added to the write model,
/// then another, until the split exists only in the architecture diagram. The
/// refusal makes the boundary code rather than convention.
/// </summary>
public sealed class OrderWriteModel
{
    private readonly Dictionary<string, PlaceOrder> orders = [];

    /// <summary>How many orders the write model holds.</summary>
    public int Count => orders.Count;

    /// <summary>Executes a command, mutating current state.</summary>
    public void Execute(PlaceOrder command)
    {
        if (orders.ContainsKey(command.OrderId))
        {
            throw new InvalidOperationException(
                $"Order '{command.OrderId}' has already been placed.");
        }

        orders[command.OrderId] = command;
    }

    /// <summary>
    /// Refused. Summaries are the read model's to serve; this member exists so
    /// the refusal is visible in the API rather than implied by an absence.
    /// </summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public OrderSummary? SummaryFor(string customerId) =>
        throw new NotSupportedException(
            $"The write model does not answer queries. Ask {nameof(OrderReadModel)} " +
            $"for the summary of '{customerId}'.");

    /// <summary>The projection's feed: current state, in write shape.</summary>
    public IEnumerable<PlaceOrder> Snapshot() => orders.Values;
}

/// <summary>
/// The query side: summaries in the shape the dashboard asks, precomputed.
///
/// **Only the projection writes here.** It is stale from the moment a command
/// lands until the projection next runs, and that staleness is the price of
/// serving reads from a model shaped purely for them.
/// </summary>
public sealed class OrderReadModel
{
    private Dictionary<string, OrderSummary> summaries = [];

    /// <summary>How many customers have a summary.</summary>
    public int Customers => summaries.Count;

    /// <summary>Answers for one customer, from the precomputed shape alone.</summary>
    public OrderSummary? SummaryFor(string customerId) =>
        summaries.TryGetValue(customerId, out OrderSummary summary) ? summary : null;

    /// <summary>Replaces the served summaries. The projection's call, nobody else's.</summary>
    public void Publish(Dictionary<string, OrderSummary> rebuilt)
    {
        ArgumentNullException.ThrowIfNull(rebuilt);
        summaries = rebuilt;
    }
}

/// <summary>
/// Moves what the write model knows into the shape the read model serves.
///
/// A full rebuild into a new dictionary, swapped in whole — the same
/// build-and-swap as a materialised view, because the read model is one: it is
/// derived, disposable, and only as current as this run.
/// </summary>
public sealed class OrderProjection
{
    private readonly OrderWriteModel writeModel;
    private readonly OrderReadModel readModel;

    /// <summary>Creates a projection from <paramref name="writeModel"/> into <paramref name="readModel"/>.</summary>
    public OrderProjection(OrderWriteModel writeModel, OrderReadModel readModel)
    {
        ArgumentNullException.ThrowIfNull(writeModel);
        ArgumentNullException.ThrowIfNull(readModel);
        this.writeModel = writeModel;
        this.readModel = readModel;
    }

    /// <summary>Recomputes every summary from the write model as it stands now.</summary>
    public void Run()
    {
        Dictionary<string, OrderSummary> rebuilt = [];

        foreach (PlaceOrder order in writeModel.Snapshot())
        {
            OrderSummary current = rebuilt.TryGetValue(order.CustomerId, out OrderSummary existing)
                ? existing
                : new OrderSummary(order.CustomerId, 0, 0m);

            rebuilt[order.CustomerId] = current with
            {
                Orders = current.Orders + 1,
                Total = current.Total + order.Amount,
            };
        }

        readModel.Publish(rebuilt);
    }
}
