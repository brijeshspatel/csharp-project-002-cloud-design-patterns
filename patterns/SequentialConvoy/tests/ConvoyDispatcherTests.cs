namespace SequentialConvoy.Tests;

/// <summary>
/// What a convoy dispatcher guarantees: strict order **within** a group, and
/// complete independence **between** groups.
///
/// The second half is what makes the pattern worth having. Global ordering is
/// easy — one consumer, one queue — and it throws away all parallelism. This
/// keeps order only where order means something.
/// </summary>
public class ConvoyDispatcherTests
{
    private static readonly string[] AccountAInOrder = ["A:1", "A:2", "A:3"];

    private static ConvoyMessage Message(string group, int sequence) =>
        new(group, sequence, $"{group}:{sequence}");

    [Fact]
    public void Delivers_a_group_in_sequence()
    {
        ConvoyDispatcher dispatcher = new();

        dispatcher.Accept(Message("A", 1));
        dispatcher.Accept(Message("A", 2));
        dispatcher.Accept(Message("A", 3));

        Assert.Equal(AccountAInOrder, dispatcher.Released.Select(m => m.Payload));
    }

    [Fact]
    public void Holds_a_message_that_arrives_out_of_order()
    {
        ConvoyDispatcher dispatcher = new();

        // Sequence 2 arrives first. Releasing it would put the ledger entries
        // in the wrong order, which for a balance is a wrong answer rather than
        // an untidy one.
        dispatcher.Accept(Message("A", 2));

        Assert.Empty(dispatcher.Released);
        Assert.Equal(1, dispatcher.HeldFor("A"));
    }

    [Fact]
    public void Releases_held_messages_once_the_gap_is_filled()
    {
        ConvoyDispatcher dispatcher = new();

        dispatcher.Accept(Message("A", 3));
        dispatcher.Accept(Message("A", 2));
        Assert.Empty(dispatcher.Released);

        dispatcher.Accept(Message("A", 1));

        // All three release at once, in order, the moment the gap closes.
        Assert.Equal(AccountAInOrder, dispatcher.Released.Select(m => m.Payload));
        Assert.Equal(0, dispatcher.HeldFor("A"));
    }

    [Fact]
    public void Does_not_let_one_blocked_group_block_another()
    {
        ConvoyDispatcher dispatcher = new();

        // Account A is missing sequence 1 and can make no progress at all.
        dispatcher.Accept(Message("A", 2));
        dispatcher.Accept(Message("A", 3));

        dispatcher.Accept(Message("B", 1));
        dispatcher.Accept(Message("B", 2));

        // B is unaffected. A single ordered queue would have stalled B behind
        // A's gap, which is the failure this pattern exists to avoid.
        Assert.Equal(["B:1", "B:2"], dispatcher.Released.Select(m => m.Payload));
        Assert.Equal(2, dispatcher.HeldFor("A"));
    }

    [Fact]
    public void Keeps_groups_independent_of_how_arrivals_interleave()
    {
        ConvoyDispatcher interleaved = new();
        foreach (ConvoyMessage message in new[]
                 {
                     Message("A", 2), Message("B", 1), Message("A", 1),
                     Message("B", 2), Message("A", 3),
                 })
        {
            interleaved.Accept(message);
        }

        ConvoyDispatcher grouped = new();
        foreach (ConvoyMessage message in new[]
                 {
                     Message("A", 1), Message("A", 2), Message("A", 3),
                     Message("B", 1), Message("B", 2),
                 })
        {
            grouped.Accept(message);
        }

        // Whatever the interleaving, each group's own order is identical.
        Assert.Equal(
            grouped.Released.Where(m => m.Group == "A").Select(m => m.Payload),
            interleaved.Released.Where(m => m.Group == "A").Select(m => m.Payload));
        Assert.Equal(
            grouped.Released.Where(m => m.Group == "B").Select(m => m.Payload),
            interleaved.Released.Where(m => m.Group == "B").Select(m => m.Payload));
    }

    [Fact]
    public void Drops_a_redelivered_message_the_group_has_already_released()
    {
        ConvoyDispatcher dispatcher = new();

        dispatcher.Accept(Message("A", 1));
        dispatcher.Accept(Message("A", 2));

        // A retry redelivers sequence 1 after the group has moved past it.
        // Holding it would poison the held set - nothing could ever release
        // it, so HeldCount would report this healthy group as stuck for ever.
        dispatcher.Accept(Message("A", 1));

        Assert.Equal(0, dispatcher.HeldFor("A"));
        Assert.Equal(2, dispatcher.Released.Count);
    }
}
