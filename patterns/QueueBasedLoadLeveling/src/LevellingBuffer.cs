namespace QueueBasedLoadLeveling;

/// <summary>
/// A bounded buffer between a producer that bursts and a service that cannot.
///
/// **Bounded is the load-bearing word.** An unbounded buffer does not level
/// load: it accepts everything, grows without limit, and converts a refusal you
/// could have handled into an out-of-memory failure you cannot — while hiding
/// how far behind the consumer has fallen. A queue that can refuse tells you the
/// truth at the moment it stops coping.
///
/// This is the pattern's own primitive, not a shared one. Tier 2 has five
/// patterns that are loosely "a queue", and each is shaped to the guarantee it
/// provides; a common buffer would make four of them the same class.
/// </summary>
public sealed class LevellingBuffer
{
    private readonly Queue<Order> buffer = new();

    /// <summary>Creates a queue holding at most <paramref name="capacity"/> orders.</summary>
    public LevellingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    /// <summary>The most it will hold.</summary>
    public int Capacity { get; }

    /// <summary>How many are waiting.</summary>
    public int Count => buffer.Count;

    /// <summary>Buffers an order, unless the queue is full.</summary>
    /// <returns><c>false</c> when the queue is full — back-pressure, not an exception.</returns>
    public bool TryEnqueue(Order order)
    {
        if (buffer.Count >= Capacity)
        {
            return false;
        }

        buffer.Enqueue(order);
        return true;
    }

    /// <summary>Takes the next order, if there is one.</summary>
    public bool TryDequeue(out Order order)
    {
        if (buffer.Count == 0)
        {
            order = default;
            return false;
        }

        order = buffer.Dequeue();
        return true;
    }

    /// <summary>
    /// Hands the service as much as it will take this cycle, and no more.
    ///
    /// This is the levelling. The producer's rate and the consumer's rate are
    /// now unrelated: the queue holds the difference, and the service is never
    /// asked a question it must refuse.
    /// </summary>
    /// <returns>How many orders were handed over.</returns>
    public int DrainInto(OrderService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        int drained = 0;
        while (service.RemainingThisCycle > 0 && TryDequeue(out Order order))
        {
            service.Accept(order);
            drained++;
        }

        return drained;
    }
}
