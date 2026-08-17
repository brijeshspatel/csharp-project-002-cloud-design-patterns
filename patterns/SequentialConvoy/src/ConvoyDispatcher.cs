namespace SequentialConvoy;

/// <summary>
/// A message belonging to an ordered group.
/// </summary>
/// <param name="Group">What it must be ordered with respect to — here, an account.</param>
/// <param name="Sequence">Its position in that group, from one.</param>
/// <param name="Payload">What it says.</param>
public readonly record struct ConvoyMessage(string Group, int Sequence, string Payload);

/// <summary>
/// Delivers each group's messages in order, and lets the groups run
/// independently of one another.
///
/// The two halves matter equally. **Order within a group** is the requirement:
/// ledger entries for one account applied out of order give a wrong balance,
/// not an untidy one. **Independence between groups** is what stops the cure
/// being worse than the disease — a single ordered queue also guarantees order,
/// and throws away every scrap of parallelism, so one account waiting for a
/// delayed message stalls every other account in the system.
///
/// This is not the same problem as Idempotent Consumer, though both concern
/// delivery semantics. That one suppresses **duplicates**; this one restores
/// **order**. Neither delivers exactly-once, which does not exist.
/// </summary>
public sealed class ConvoyDispatcher
{
    private readonly Dictionary<string, int> nextExpected = [];
    private readonly Dictionary<string, SortedDictionary<int, ConvoyMessage>> held = [];
    private readonly List<ConvoyMessage> released = [];

    /// <summary>Everything delivered so far, in the order it was delivered.</summary>
    public IReadOnlyList<ConvoyMessage> Released => released;

    /// <summary>How many messages are waiting for a gap to close, across all groups.</summary>
    public int HeldCount => held.Values.Sum(group => group.Count);

    /// <summary>How many messages <paramref name="group"/> is holding.</summary>
    public int HeldFor(string group) =>
        held.TryGetValue(group, out SortedDictionary<int, ConvoyMessage>? waiting)
            ? waiting.Count
            : 0;

    /// <summary>
    /// Takes a message, releasing it and anything now contiguous behind it.
    /// </summary>
    public void Accept(ConvoyMessage message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Group);
        ArgumentOutOfRangeException.ThrowIfLessThan(message.Sequence, 1);

        if (!held.TryGetValue(message.Group, out SortedDictionary<int, ConvoyMessage>? waiting))
        {
            waiting = [];
            held[message.Group] = waiting;
            nextExpected[message.Group] = 1;
        }

        // A sequence the group has already released is a redelivery. Holding it
        // would poison the held set - nothing could ever release it, so
        // HeldCount, the number an operator watches for stuck convoys, would
        // climb for ever on a healthy group. Dropping it is safe precisely
        // because it was released once already; suppressing what a duplicate
        // *does* remains Idempotent Consumer's job.
        if (message.Sequence < nextExpected[message.Group])
        {
            return;
        }

        waiting[message.Sequence] = message;

        // Release as far as the sequence is unbroken. A gap stops **this**
        // group and nothing else: every other group has its own expectation and
        // its own held set, which is the independence the pattern promises.
        while (waiting.TryGetValue(nextExpected[message.Group], out ConvoyMessage next))
        {
            released.Add(next);
            waiting.Remove(nextExpected[message.Group]);
            nextExpected[message.Group]++;
        }
    }
}
