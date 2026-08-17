namespace IdempotentConsumer.Tests;

/// <summary>
/// What an idempotent handler guarantees: that processing a message twice has
/// the same effect as processing it once, and — the part that is easy to get
/// wrong — that a message whose work **failed** is not recorded as done.
/// </summary>
public class IdempotentHandlerTests
{
    private static PaymentCapture Capture(string messageId = "MSG-001") =>
        new(messageId, "ACC-42", 25.00m);

    [Fact]
    public void Processes_a_message_it_has_not_seen()
    {
        Ledger ledger = new();
        IdempotentHandler handler = new(new InMemoryProcessedMessageLog());

        HandlingResult result = handler.Handle(Capture(), ledger.Debit);

        Assert.Equal(HandlingOutcome.Processed, result.Outcome);
        Assert.Equal(25.00m, ledger.DebitedFrom("ACC-42"));
    }

    [Fact]
    public void Ignores_a_repeat_of_a_message_it_has_already_processed()
    {
        Ledger ledger = new();
        IdempotentHandler handler = new(new InMemoryProcessedMessageLog());

        handler.Handle(Capture(), ledger.Debit);
        HandlingResult second = handler.Handle(Capture(), ledger.Debit);

        // The account is debited once. This is the whole pattern: at-least-once
        // delivery means the same capture will arrive again.
        Assert.Equal(HandlingOutcome.Duplicate, second.Outcome);
        Assert.Equal(25.00m, ledger.DebitedFrom("ACC-42"));
    }

    [Fact]
    public void Records_the_message_only_after_the_work_succeeds()
    {
        Ledger ledger = new();
        InMemoryProcessedMessageLog log = new();
        IdempotentHandler handler = new(log);

        Assert.Throws<InvalidOperationException>(() =>
            handler.Handle(Capture(), _ => throw new InvalidOperationException("ledger unavailable")));

        // Recording before doing the work would lose it silently: the
        // redelivery would be suppressed as a duplicate and the account would
        // never be debited at all.
        Assert.False(log.HasProcessed("MSG-001"));

        HandlingResult retry = handler.Handle(Capture(), ledger.Debit);

        Assert.Equal(HandlingOutcome.Processed, retry.Outcome);
        Assert.Equal(25.00m, ledger.DebitedFrom("ACC-42"));
    }

    [Fact]
    public void Treats_two_different_messages_independently()
    {
        Ledger ledger = new();
        IdempotentHandler handler = new(new InMemoryProcessedMessageLog());

        handler.Handle(Capture("MSG-001"), ledger.Debit);
        HandlingResult second = handler.Handle(Capture("MSG-002"), ledger.Debit);

        Assert.Equal(HandlingOutcome.Processed, second.Outcome);
        Assert.Equal(50.00m, ledger.DebitedFrom("ACC-42"));
    }

    [Fact]
    public void Returns_the_original_outcome_for_a_duplicate()
    {
        Ledger ledger = new();
        IdempotentHandler handler = new(new InMemoryProcessedMessageLog());

        HandlingResult first = handler.Handle(Capture(), ledger.Debit);
        HandlingResult second = handler.Handle(Capture(), ledger.Debit);

        // The caller gets the same answer both times. Returning nothing, or an
        // error, would make the sender think the capture had failed and retry
        // it for ever.
        Assert.Equal(first.Effect, second.Effect);
    }
}
