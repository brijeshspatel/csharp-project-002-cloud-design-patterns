namespace PriorityQueue;

/// <summary>How urgent a piece of work is. Declared most urgent first.</summary>
public enum Priority
{
    /// <summary>Served before everything else.</summary>
    High,

    /// <summary>The default.</summary>
    Normal,

    /// <summary>Served only when nothing more urgent is waiting.</summary>
    Low,
}

/// <summary>A support request waiting to be worked on.</summary>
/// <param name="Reference">What the customer quotes.</param>
/// <param name="Customer">Who raised it.</param>
public readonly record struct SupportTicket(string Reference, string Customer);

/// <summary>
/// Work served by importance rather than arrival.
///
/// It is called a **backlog** rather than a queue for two reasons: the analyser
/// reserves the <c>Queue</c> suffix for types deriving from <c>Queue</c>, and
/// "backlog" is the more honest word — the low-priority end of this is precisely
/// a backlog, and naming it so makes the starvation risk harder to forget.
///
/// **Arrival order still decides between equals.** One queue per priority, each
/// FIFO, is what keeps "priority" from degenerating into "arbitrary": two
/// customers on the same tier are treated identically.
///
/// This is the pattern's own primitive. Tier 2 has several patterns that are
/// loosely "a queue" and each is shaped to its own guarantee; a shared buffer
/// would make them the same class.
/// </summary>
public sealed class PriorityBacklog
{
    // Declared most urgent first, and drained in declaration order. Relying on
    // the enum's order keeps the two definitions from drifting apart.
    private readonly Dictionary<Priority, Queue<SupportTicket>> lanes =
        Enum.GetValues<Priority>().ToDictionary(p => p, _ => new Queue<SupportTicket>());

    /// <summary>How many are waiting, at every priority.</summary>
    public int Count => lanes.Values.Sum(lane => lane.Count);

    /// <summary>How many are waiting at <paramref name="priority"/>.</summary>
    public int CountAt(Priority priority) => lanes[priority].Count;

    /// <summary>Adds a ticket at <paramref name="priority"/>.</summary>
    public void Enqueue(SupportTicket ticket, Priority priority) =>
        lanes[priority].Enqueue(ticket);

    /// <summary>
    /// Takes the most urgent ticket waiting, oldest first among equals.
    /// </summary>
    /// <returns><c>false</c> when nothing is waiting at any priority.</returns>
    public bool TryDequeue(out SupportTicket ticket)
    {
        foreach (Priority priority in Enum.GetValues<Priority>())
        {
            if (lanes[priority].Count > 0)
            {
                ticket = lanes[priority].Dequeue();
                return true;
            }
        }

        ticket = default;
        return false;
    }
}
