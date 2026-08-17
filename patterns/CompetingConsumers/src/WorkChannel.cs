namespace CompetingConsumers;

/// <summary>A unit of work — here, one uploaded image awaiting a thumbnail.</summary>
/// <param name="Reference">What identifies it.</param>
public readonly record struct WorkItem(string Reference);

/// <summary>
/// One channel, many consumers, and **each message claimed by exactly one** of
/// them.
///
/// That guarantee is the entire difference between this pattern and
/// Publisher-Subscriber, where every subscriber receives every message. They are
/// the two answers to "who receives this?", and choosing wrongly is expensive in
/// opposite directions: a topic used for work does it N times; a channel used
/// for events tells only one listener something they all needed to know.
///
/// Claiming **removes** the item. Releasing puts it back. A real broker does the
/// same thing with a visibility timeout, so that a consumer which dies holding a
/// message does not take the message with it.
/// </summary>
public sealed class WorkChannel
{
    private readonly Queue<WorkItem> pending = new();

    /// <summary>How many items are waiting to be claimed.</summary>
    public int Count => pending.Count;

    /// <summary>Puts work on the channel.</summary>
    public void Post(WorkItem item) => pending.Enqueue(item);

    /// <summary>
    /// Takes the next item, removing it so no other consumer can take it.
    /// </summary>
    /// <returns><c>false</c> when there is nothing to claim.</returns>
    public bool TryClaim(out WorkItem item)
    {
        if (pending.Count == 0)
        {
            item = default;
            return false;
        }

        item = pending.Dequeue();
        return true;
    }

    /// <summary>
    /// Puts a claimed item back, because the consumer could not finish it.
    ///
    /// It goes to the back rather than the front, deliberately: a message that
    /// fails repeatedly should not block everything behind it while it is
    /// retried. A real broker adds a delivery count and eventually dead-letters
    /// it, which this model does not.
    /// </summary>
    public void Release(WorkItem item) => pending.Enqueue(item);
}
