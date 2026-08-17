namespace PublisherSubscriber.Tests;

/// <summary>
/// What a topic guarantees: that **every** subscriber receives **every** event,
/// that one broken subscriber cannot stop the others or the publisher, and that
/// the publisher never learns who is listening.
///
/// The first of those is the exact opposite of Competing Consumers, where each
/// message goes to exactly one consumer. Choosing wrongly between the two is a
/// common and expensive design error, so both are built here as opposites
/// rather than as one primitive with a flag.
/// </summary>
public class TopicTests
{
    private static OrderPlaced Order(string reference = "ORD-001") => new(reference, 42.50m);

    [Fact]
    public void Delivers_every_event_to_every_subscriber()
    {
        Topic topic = new();
        List<string> fulfilment = [];
        List<string> billing = [];
        List<string> analytics = [];

        topic.Subscribe("fulfilment", order => fulfilment.Add(order.Reference));
        topic.Subscribe("billing", order => billing.Add(order.Reference));
        topic.Subscribe("analytics", order => analytics.Add(order.Reference));

        topic.Publish(Order("ORD-001"));
        topic.Publish(Order("ORD-002"));

        // Every subscriber saw both. Under Competing Consumers each order would
        // have gone to one of the three.
        Assert.Equal(2, fulfilment.Count);
        Assert.Equal(2, billing.Count);
        Assert.Equal(2, analytics.Count);
    }

    [Fact]
    public void Stops_delivering_to_a_subscriber_that_unsubscribed()
    {
        Topic topic = new();
        List<string> received = [];

        IDisposable subscription = topic.Subscribe("analytics", o => received.Add(o.Reference));
        topic.Publish(Order("ORD-001"));

        subscription.Dispose();
        topic.Publish(Order("ORD-002"));

        Assert.Equal(["ORD-001"], received);
        Assert.Equal(0, topic.SubscriberCount);
    }

    [Fact]
    public void Isolates_a_subscriber_that_throws_from_the_others()
    {
        Topic topic = new();
        List<string> fulfilment = [];
        List<string> analytics = [];

        topic.Subscribe("fulfilment", order => fulfilment.Add(order.Reference));
        topic.Subscribe("billing", _ => throw new InvalidOperationException("billing is down"));
        topic.Subscribe("analytics", order => analytics.Add(order.Reference));

        PublishResult result = topic.Publish(Order());

        // The failing subscriber is in the middle deliberately: an
        // implementation that let the exception escape would never reach
        // analytics, and only this ordering catches that.
        Assert.Single(fulfilment);
        Assert.Single(analytics);
        Assert.Equal(2, result.Delivered);
    }

    [Fact]
    public void Delivers_nothing_published_before_a_subscriber_joined()
    {
        Topic topic = new();
        topic.Publish(Order("ORD-001"));

        List<string> received = [];
        topic.Subscribe("late", order => received.Add(order.Reference));
        topic.Publish(Order("ORD-002"));

        // A topic is not a log. A late subscriber gets what happens next, not
        // what it missed - which is the difference between this and Event
        // Sourcing.
        Assert.Equal(["ORD-002"], received);
    }

    [Fact]
    public void Publishes_successfully_when_nobody_is_listening()
    {
        Topic topic = new();

        PublishResult result = topic.Publish(Order());

        // The publisher does not know or care whether anyone subscribed. That
        // ignorance is the decoupling the pattern exists to provide.
        Assert.Equal(0, result.Delivered);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Reports_delivery_failures_without_failing_the_publish()
    {
        Topic topic = new();
        topic.Subscribe("billing", _ => throw new InvalidOperationException("billing is down"));

        PublishResult result = topic.Publish(Order());

        Assert.Equal(0, result.Delivered);
        SubscriberFailure failure = Assert.Single(result.Failures);
        Assert.Equal("billing", failure.Subscriber);
        Assert.Contains("billing is down", failure.Reason, StringComparison.Ordinal);
    }
}
