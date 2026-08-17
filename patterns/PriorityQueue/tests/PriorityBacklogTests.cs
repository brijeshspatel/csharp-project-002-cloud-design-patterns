namespace PriorityQueue.Tests;

/// <summary>
/// What a priority backlog guarantees: that importance beats arrival, and that
/// arrival still decides between equals.
///
/// The second half is what stops "priority" becoming "arbitrary". Within one
/// priority the order is first in, first out, so two customers on the same tier
/// are treated the same way.
/// </summary>
public class PriorityBacklogTests
{
    private static readonly string[] ArrivalOrderWithinNormal =
        ["TCK-001", "TCK-002", "TCK-003"];

    private static SupportTicket Ticket(int number, string customer = "contoso") =>
        new($"TCK-{number:000}", customer);

    [Fact]
    public void Serves_a_higher_priority_before_a_lower_one()
    {
        PriorityBacklog backlog = new();
        backlog.Enqueue(Ticket(1), Priority.Low);
        backlog.Enqueue(Ticket(2), Priority.High);
        backlog.Enqueue(Ticket(3), Priority.Normal);

        Assert.True(backlog.TryDequeue(out SupportTicket first));
        Assert.Equal("TCK-002", first.Reference);

        Assert.True(backlog.TryDequeue(out SupportTicket second));
        Assert.Equal("TCK-003", second.Reference);

        Assert.True(backlog.TryDequeue(out SupportTicket third));
        Assert.Equal("TCK-001", third.Reference);
    }

    [Fact]
    public void Preserves_arrival_order_within_a_priority()
    {
        PriorityBacklog backlog = new();
        backlog.Enqueue(Ticket(1), Priority.Normal);
        backlog.Enqueue(Ticket(2), Priority.Normal);
        backlog.Enqueue(Ticket(3), Priority.Normal);

        List<string> served = [];
        while (backlog.TryDequeue(out SupportTicket ticket))
        {
            served.Add(ticket.Reference);
        }

        Assert.Equal(ArrivalOrderWithinNormal, served);
    }

    [Fact]
    public void Serves_a_late_high_priority_item_before_an_early_low_one()
    {
        PriorityBacklog backlog = new();

        // Five low-priority tickets arrive first and wait.
        for (int i = 1; i <= 5; i++)
        {
            backlog.Enqueue(Ticket(i), Priority.Low);
        }

        // A paid-tier ticket arrives last. It is served first, which is the
        // entire point: arrival order is not the same as importance.
        backlog.Enqueue(Ticket(99, "fabrikam"), Priority.High);

        Assert.True(backlog.TryDequeue(out SupportTicket next));
        Assert.Equal("TCK-099", next.Reference);
    }

    [Fact]
    public void Reports_empty_when_nothing_is_queued()
    {
        PriorityBacklog backlog = new();

        Assert.False(backlog.TryDequeue(out SupportTicket _));
        Assert.Equal(0, backlog.Count);
    }

    [Fact]
    public void Counts_what_is_waiting_at_each_priority()
    {
        PriorityBacklog backlog = new();
        backlog.Enqueue(Ticket(1), Priority.High);
        backlog.Enqueue(Ticket(2), Priority.Low);
        backlog.Enqueue(Ticket(3), Priority.Low);

        // Per-priority depth is the number that reveals starvation: a low queue
        // growing while a high queue stays empty is the signature.
        Assert.Equal(1, backlog.CountAt(Priority.High));
        Assert.Equal(0, backlog.CountAt(Priority.Normal));
        Assert.Equal(2, backlog.CountAt(Priority.Low));
        Assert.Equal(3, backlog.Count);
    }
}
