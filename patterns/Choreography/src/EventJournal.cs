namespace Choreography;

/// <summary>
/// Something that happened to an order, announced rather than commanded.
///
/// The distinction is the pattern: `OrderPlaced` states a fact and asks nothing
/// of anybody. A command — `ReserveStock` — names its recipient and the sequence
/// with it, which is orchestration. Events let the publisher stay ignorant of
/// who, if anyone, cares.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="OrderId">Which order it happened to.</param>
public readonly record struct OrderEvent(string Kind, string OrderId)
{
    /// <summary>An order was placed.</summary>
    public const string Placed = "OrderPlaced";

    /// <summary>Payment went through.</summary>
    public const string PaymentAccepted = "PaymentAccepted";

    /// <summary>Stock was set aside.</summary>
    public const string StockReserved = "StockReserved";

    /// <summary>It went out of the door.</summary>
    public const string Shipped = "Shipped";
}

/// <summary>
/// How events reach whoever cares. **It is a transport, not a coordinator**: it
/// holds no sequence, decides nothing, and would be equally happy if no service
/// subscribed at all.
///
/// Its ordered history exists so this model can be inspected. A real broker
/// gives each subscriber its own stream and no single view of everything, so
/// treat that history as scaffolding rather than as something the pattern
/// provides.
/// </summary>
public sealed class EventJournal
{
    private readonly Dictionary<string, List<Action<OrderEvent>>> subscribers = [];
    private readonly List<OrderEvent> history = [];

    /// <summary>Every event published, oldest first — scaffolding for inspection.</summary>
    public IReadOnlyList<OrderEvent> History => history;

    /// <summary>Registers interest in one kind of event.</summary>
    public void Subscribe(string kind, Action<OrderEvent> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(handler);

        if (!subscribers.TryGetValue(kind, out List<Action<OrderEvent>>? handlers))
        {
            handlers = [];
            subscribers[kind] = handlers;
        }

        handlers.Add(handler);
    }

    /// <summary>
    /// Announces an event to whoever subscribed to that kind. The publisher is
    /// never told who received it, or whether anybody did.
    /// </summary>
    public void Publish(OrderEvent announced)
    {
        history.Add(announced);

        if (!subscribers.TryGetValue(announced.Kind, out List<Action<OrderEvent>>? handlers))
        {
            return;
        }

        // Copied before iterating: a handler may publish, which may subscribe.
        foreach (Action<OrderEvent> handler in handlers.ToList())
        {
            handler(announced);
        }
    }
}

/// <summary>
/// Takes payment when an order is placed, and announces that it did.
///
/// **It does not know what happens next**, and that ignorance is the point. It
/// has never heard of inventory or shipping; adding a fraud check downstream
/// would not change a line of this class.
/// </summary>
public sealed class PaymentService
{
    private readonly EventJournal journal;
    private readonly List<string> handled = [];

    /// <summary>Subscribes to the one event it cares about.</summary>
    public PaymentService(EventJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        this.journal = journal;
        journal.Subscribe(OrderEvent.Placed, OnOrderPlaced);
    }

    /// <summary>The event kinds this service has reacted to.</summary>
    public IReadOnlyList<string> Handled => handled;

    /// <summary>Whether it has seen <paramref name="kind"/> at all.</summary>
    public bool Knows(string kind) => handled.Contains(kind);

    private void OnOrderPlaced(OrderEvent placed)
    {
        handled.Add(placed.Kind);
        journal.Publish(new OrderEvent(OrderEvent.PaymentAccepted, placed.OrderId));
    }
}

/// <summary>
/// Reserves stock once payment is accepted — or, where there is none,
/// **announces nothing at all**.
///
/// Publishing nothing is a decision this service makes alone, and it is how a
/// choreographed operation stops: not with a failure that propagates, but with
/// a silence that nobody is listening for.
/// </summary>
public sealed class InventoryService
{
    private readonly EventJournal journal;
    private readonly bool stockAvailable;
    private readonly List<string> handled = [];

    /// <summary>Subscribes to the one event it cares about.</summary>
    public InventoryService(EventJournal journal, bool stockAvailable)
    {
        ArgumentNullException.ThrowIfNull(journal);
        this.journal = journal;
        this.stockAvailable = stockAvailable;
        journal.Subscribe(OrderEvent.PaymentAccepted, OnPaymentAccepted);
    }

    /// <summary>The event kinds this service has reacted to.</summary>
    public IReadOnlyList<string> Handled => handled;

    /// <summary>Whether it has seen <paramref name="kind"/> at all.</summary>
    public bool Knows(string kind) => handled.Contains(kind);

    private void OnPaymentAccepted(OrderEvent accepted)
    {
        handled.Add(accepted.Kind);

        if (!stockAvailable)
        {
            return;
        }

        journal.Publish(new OrderEvent(OrderEvent.StockReserved, accepted.OrderId));
    }
}

/// <summary>
/// Ships once stock is reserved.
///
/// **It waits on stock, not on payment.** Subscribing to the wrong event is the
/// characteristic choreography defect: nothing rejects it, no type changes, and
/// the operation quietly runs in an order nobody intended.
/// </summary>
public sealed class ShippingService
{
    private readonly EventJournal journal;
    private readonly List<string> handled = [];

    /// <summary>Subscribes to the one event it cares about.</summary>
    public ShippingService(EventJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        this.journal = journal;
        journal.Subscribe(OrderEvent.StockReserved, OnStockReserved);
    }

    /// <summary>The event kinds this service has reacted to.</summary>
    public IReadOnlyList<string> Handled => handled;

    /// <summary>Whether it has seen <paramref name="kind"/> at all.</summary>
    public bool Knows(string kind) => handled.Contains(kind);

    private void OnStockReserved(OrderEvent reserved)
    {
        handled.Add(reserved.Kind);
        journal.Publish(new OrderEvent(OrderEvent.Shipped, reserved.OrderId));
    }
}
