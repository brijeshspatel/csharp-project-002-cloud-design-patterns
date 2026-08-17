namespace PublisherSubscriber;

/// <summary>Something that happened, announced to whoever cares.</summary>
/// <param name="Reference">Which order.</param>
/// <param name="Amount">What it came to.</param>
public readonly record struct OrderPlaced(string Reference, decimal Amount);

/// <summary>A subscriber that threw while handling an event.</summary>
/// <param name="Subscriber">Which one.</param>
/// <param name="Reason">What it said.</param>
public readonly record struct SubscriberFailure(string Subscriber, string Reason);

/// <summary>What came of a publish.</summary>
/// <param name="Delivered">How many subscribers handled it successfully.</param>
/// <param name="Failures">Which ones did not, and why.</param>
public sealed record PublishResult(int Delivered, IReadOnlyList<SubscriberFailure> Failures);

/// <summary>
/// Announces events to every subscriber, and tells the publisher nothing about
/// who they are.
///
/// **Every subscriber receives every event.** That is the exact opposite of
/// Competing Consumers, where each message goes to exactly one consumer, and
/// the two are the answers to the same question — "who receives this?" —
/// pointing in opposite directions. Using a topic where work should have been
/// split does the work N times; using a channel where an event should have been
/// announced tells one listener something they all needed to know.
///
/// A topic is **not a log**. A subscriber that joins late receives what happens
/// next, not what it missed. Replay is Event Sourcing's job.
/// </summary>
public sealed class Topic
{
    private readonly List<Subscription> subscriptions = [];

    /// <summary>How many are currently listening.</summary>
    public int SubscriberCount => subscriptions.Count;

    /// <summary>
    /// Registers <paramref name="handler"/> under <paramref name="name"/>.
    /// </summary>
    /// <returns>Dispose it to stop receiving events.</returns>
    public IDisposable Subscribe(string name, Action<OrderPlaced> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);

        Subscription subscription = new(this, name, handler);
        subscriptions.Add(subscription);
        return subscription;
    }

    /// <summary>
    /// Announces <paramref name="order"/> to everyone listening.
    ///
    /// It never throws on a subscriber's behalf. A publisher brought down by a
    /// subscriber it has never heard of is coupling reintroduced through the
    /// back door — and the whole point of the pattern is that the publisher does
    /// not depend on the subscribers.
    /// </summary>
    public PublishResult Publish(OrderPlaced order)
    {
        int delivered = 0;
        List<SubscriberFailure> failures = [];

        // A copy, so a handler that subscribes or unsubscribes during delivery
        // cannot corrupt the iteration.
        foreach (Subscription subscription in subscriptions.ToArray())
        {
            try
            {
                subscription.Handler(order);
                delivered++;
            }
            catch (Exception failure)
            {
                // Reported, never propagated. One broken subscriber must not
                // stop the ones after it, which is why the demonstration puts
                // the failing subscriber in the middle.
                failures.Add(new SubscriberFailure(subscription.Name, failure.Message));
            }
        }

        return new PublishResult(delivered, failures);
    }

    private void Remove(Subscription subscription) => subscriptions.Remove(subscription);

    private sealed class Subscription : IDisposable
    {
        private Topic? topic;

        public Subscription(Topic topic, string name, Action<OrderPlaced> handler)
        {
            this.topic = topic;
            Name = name;
            Handler = handler;
        }

        public string Name { get; }

        public Action<OrderPlaced> Handler { get; }

        public void Dispose()
        {
            Topic? owner = topic;
            topic = null;
            owner?.Remove(this);
        }
    }
}
