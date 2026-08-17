namespace CompetingConsumers.Tests;

/// <summary>
/// What competing consumers guarantee: that every message is handled by
/// **exactly one** consumer, that adding consumers gets through a backlog
/// faster, and that a consumer failing does not lose the message it held.
///
/// Consumers are pumped explicitly rather than raced. What is being asserted is
/// the distribution guarantee, not the thread scheduler — a test that spawned
/// tasks would be asserting the latter and would fail on a busy machine.
/// </summary>
public class WorkChannelTests
{
    private static WorkChannel ChannelOf(int count)
    {
        WorkChannel channel = new();
        for (int i = 1; i <= count; i++)
        {
            channel.Post(new WorkItem($"IMG-{i:000}"));
        }

        return channel;
    }

    [Fact]
    public void Delivers_each_message_to_exactly_one_consumer()
    {
        WorkChannel channel = ChannelOf(6);
        Consumer[] consumers = [new("worker-1"), new("worker-2"), new("worker-3")];

        // Bounded deliberately. A claim that failed to remove its item would
        // make an unbounded drain loop spin for ever, and a test that hangs is
        // worse than one that fails: CI times out with nothing to diagnose.
        int guard = 0;
        while (consumers.Any(consumer => consumer.ProcessNext(channel)))
        {
            Assert.True(++guard <= 100, "the channel never drained; claiming is not removing");
        }

        List<string> handled = [.. consumers.SelectMany(c => c.Handled).Select(w => w.Reference)];

        Assert.Equal(6, handled.Count);
        Assert.Equal(6, handled.Distinct().Count());
        Assert.Equal(0, channel.Count);
    }

    [Fact]
    public void Shares_work_across_every_consumer()
    {
        WorkChannel channel = ChannelOf(6);
        Consumer[] consumers = [new("worker-1"), new("worker-2"), new("worker-3")];

        bool progressed = true;
        int guard = 0;
        while (progressed)
        {
            Assert.True(++guard <= 100, "the channel never drained; claiming is not removing");

            progressed = false;
            foreach (Consumer consumer in consumers)
            {
                progressed |= consumer.ProcessNext(channel);
            }
        }

        Assert.All(consumers, consumer => Assert.NotEmpty(consumer.Handled));
    }

    [Fact]
    public void Returns_a_message_to_the_channel_when_a_consumer_fails()
    {
        WorkChannel channel = ChannelOf(1);
        Consumer flaky = new("worker-1", failOnce: true);

        // The consumer takes the message, fails, and puts it back. Losing it
        // here is the bug: a claimed message that is never released and never
        // completed disappears silently.
        Assert.False(flaky.ProcessNext(channel));
        Assert.Equal(1, channel.Count);

        Consumer healthy = new("worker-2");
        Assert.True(healthy.ProcessNext(channel));
        Assert.Single(healthy.Handled);
    }

    [Fact]
    public void Reports_nothing_to_claim_when_the_channel_is_empty()
    {
        WorkChannel channel = new();
        Consumer consumer = new("worker-1");

        Assert.False(consumer.ProcessNext(channel));
        Assert.Empty(consumer.Handled);
    }

    [Fact]
    public void Processes_a_backlog_faster_with_more_consumers()
    {
        // Rounds, not wall-clock. One consumer needs one round per message;
        // three need a third of them. Deterministic, and it is the real claim
        // the pattern makes.
        Assert.Equal(12, RoundsToDrain(consumerCount: 1, backlog: 12));
        Assert.Equal(4, RoundsToDrain(consumerCount: 3, backlog: 12));
    }

    private static int RoundsToDrain(int consumerCount, int backlog)
    {
        WorkChannel channel = ChannelOf(backlog);
        Consumer[] consumers = [.. Enumerable
            .Range(1, consumerCount)
            .Select(n => new Consumer($"worker-{n}"))];

        int rounds = 0;
        while (channel.Count > 0)
        {
            rounds++;
            Assert.True(rounds <= backlog * 10, "the channel never drained; claiming is not removing");

            foreach (Consumer consumer in consumers)
            {
                consumer.ProcessNext(channel);
            }
        }

        return rounds;
    }
}
