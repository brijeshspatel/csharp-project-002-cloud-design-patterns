namespace ClaimCheck;

/// <summary>
/// Sends a payload of any size over a bus that will not carry it.
///
/// Below <c>inlineLimit</c> the payload travels on the bus as it is. Above it,
/// the payload goes to the store and a **claim check** — a small reference —
/// travels instead. The receiver redeems the check.
///
/// The threshold matters: a store round trip costs latency and money, so a short
/// note should not pay for one. This is the same reasoning that makes the
/// pattern worth having at all, applied in the other direction.
/// </summary>
public sealed class ClaimCheckSender
{
    private readonly MessageBus bus;
    private readonly IPayloadStore store;
    private readonly int inlineLimit;

    /// <summary>Creates a sender.</summary>
    /// <param name="bus">Where messages go.</param>
    /// <param name="store">Where large payloads go.</param>
    /// <param name="inlineLimit">Payloads at or below this size travel on the bus.</param>
    public ClaimCheckSender(MessageBus bus, IPayloadStore store, int inlineLimit)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentOutOfRangeException.ThrowIfLessThan(inlineLimit, 1);

        this.bus = bus;
        this.store = store;
        this.inlineLimit = inlineLimit;
    }

    /// <summary>Sends <paramref name="payload"/> under <paramref name="subject"/>.</summary>
    public void Send(string subject, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(payload);

        if (InMemoryPayloadStore.SizeOf(payload) <= inlineLimit)
        {
            bus.Send(new BusMessage(subject, payload));
            return;
        }

        string reference = store.Put(payload);
        bus.Send(new BusMessage(subject, ClaimCheckToken.For(reference)));
    }
}

/// <summary>
/// Receives messages, redeeming a claim check where it finds one.
///
/// The receiver does not need to know which it will get. That is what keeps the
/// pattern from leaking into every consumer.
/// </summary>
public sealed class ClaimCheckReceiver
{
    private readonly MessageBus bus;
    private readonly IPayloadStore store;

    /// <summary>Creates a receiver.</summary>
    public ClaimCheckReceiver(MessageBus bus, IPayloadStore store)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(store);

        this.bus = bus;
        this.store = store;
    }

    /// <summary>Takes the next message and returns its payload, whole.</summary>
    /// <exception cref="PayloadUnavailableException">
    /// The message carried a claim check whose payload has gone.
    /// </exception>
    public bool TryReceive(out string payload)
    {
        if (!bus.TryReceive(out BusMessage message))
        {
            payload = string.Empty;
            return false;
        }

        payload = ClaimCheckToken.TryRead(message.Body, out string reference)
            ? store.Retrieve(reference)
            : message.Body;

        return true;
    }
}
