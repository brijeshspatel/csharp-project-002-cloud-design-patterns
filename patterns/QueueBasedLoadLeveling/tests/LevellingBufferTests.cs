namespace QueueBasedLoadLeveling.Tests;

/// <summary>
/// What a levelling queue guarantees: that a burst is absorbed rather than
/// forwarded, that the buffer is bounded rather than infinite, and that the
/// service downstream is only ever asked for what it can take.
/// </summary>
public class LevellingBufferTests
{
    private static readonly string[] FirstThreeReferences =
        ["ORD-001", "ORD-002", "ORD-003"];

    private static Order Order(int number) => new($"ORD-{number:000}");

    [Fact]
    public void Accepts_work_up_to_its_capacity()
    {
        LevellingBuffer queue = new(capacity: 3);

        Assert.True(queue.TryEnqueue(Order(1)));
        Assert.True(queue.TryEnqueue(Order(2)));
        Assert.True(queue.TryEnqueue(Order(3)));
        Assert.Equal(3, queue.Count);
    }

    [Fact]
    public void Refuses_work_once_the_buffer_is_full()
    {
        LevellingBuffer queue = new(capacity: 2);
        queue.TryEnqueue(Order(1));
        queue.TryEnqueue(Order(2));

        // Bounded, not infinite. An unbounded buffer does not level load, it
        // defers an out-of-memory failure and hides how far behind you are.
        Assert.False(queue.TryEnqueue(Order(3)));
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void Drains_only_what_the_service_can_take()
    {
        LevellingBuffer queue = new(capacity: 10);
        OrderService service = new(perCycleCapacity: 3);

        for (int i = 1; i <= 8; i++)
        {
            queue.TryEnqueue(Order(i));
        }

        int drained = queue.DrainInto(service);

        Assert.Equal(3, drained);
        Assert.Equal(5, queue.Count);
        Assert.Equal(3, service.Accepted.Count);
    }

    [Fact]
    public void Preserves_arrival_order()
    {
        LevellingBuffer queue = new(capacity: 5);
        queue.TryEnqueue(Order(1));
        queue.TryEnqueue(Order(2));
        queue.TryEnqueue(Order(3));

        OrderService service = new(perCycleCapacity: 5);
        queue.DrainInto(service);

        Assert.Equal(
            FirstThreeReferences,
            service.Accepted.Select(order => order.Reference));
    }

    [Fact]
    public void Absorbs_a_burst_the_service_would_have_rejected()
    {
        OrderService direct = new(perCycleCapacity: 3);
        int rejectedWithoutQueue = 0;

        for (int i = 1; i <= 8; i++)
        {
            try
            {
                direct.Accept(Order(i));
            }
            catch (ServiceOverloadedException)
            {
                rejectedWithoutQueue++;
            }
        }

        Assert.Equal(5, rejectedWithoutQueue);

        // The same burst through the queue loses nothing: it is served over
        // several cycles instead of being refused in one.
        LevellingBuffer queue = new(capacity: 10);
        OrderService buffered = new(perCycleCapacity: 3);

        for (int i = 1; i <= 8; i++)
        {
            Assert.True(queue.TryEnqueue(Order(i)));
        }

        while (queue.Count > 0)
        {
            buffered.BeginCycle();
            queue.DrainInto(buffered);
        }

        Assert.Equal(8, buffered.Accepted.Count);
    }
}
