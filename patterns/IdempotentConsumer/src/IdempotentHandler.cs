namespace IdempotentConsumer;

/// <summary>A request to take money from an account.</summary>
/// <param name="MessageId">Stable across redeliveries. This is what de-duplication turns on.</param>
/// <param name="Account">Whose money.</param>
/// <param name="Amount">How much.</param>
public readonly record struct PaymentCapture(string MessageId, string Account, decimal Amount);

/// <summary>Whether the handler did the work or recognised a repeat.</summary>
public enum HandlingOutcome
{
    /// <summary>First time seen; the work was done.</summary>
    Processed,

    /// <summary>Seen before; nothing was done again.</summary>
    Duplicate,
}

/// <summary>What the handler did, and what the effect was.</summary>
/// <param name="Outcome">Processed or duplicate.</param>
/// <param name="Effect">What the work reported, replayed identically for a duplicate.</param>
public readonly record struct HandlingResult(HandlingOutcome Outcome, string Effect);

/// <summary>Remembers which messages have been fully processed.</summary>
public interface IProcessedMessageLog
{
    /// <summary>Whether <paramref name="messageId"/> has been processed.</summary>
    bool HasProcessed(string messageId);

    /// <summary>Looks up what happened when it was processed.</summary>
    bool TryGetEffect(string messageId, out string effect);

    /// <summary>Records that <paramref name="messageId"/> was processed, with its effect.</summary>
    void Record(string messageId, string effect);
}

/// <summary>An in-memory log. A real one is a table with a unique constraint.</summary>
public sealed class InMemoryProcessedMessageLog : IProcessedMessageLog
{
    private readonly Dictionary<string, string> effects = [];

    /// <inheritdoc/>
    public bool HasProcessed(string messageId) => effects.ContainsKey(messageId);

    /// <inheritdoc/>
    public bool TryGetEffect(string messageId, out string effect) =>
        effects.TryGetValue(messageId, out effect!);

    /// <inheritdoc/>
    public void Record(string messageId, string effect) => effects[messageId] = effect;
}

/// <summary>
/// Makes processing a message twice have the same effect as processing it once.
///
/// This is not an optimisation. **Exactly-once delivery does not exist** — the
/// acknowledgement can be lost after the work is done, and the sender cannot
/// tell that from the work never happening, so it retries. At-least-once
/// delivery plus idempotent processing is the closest real thing, and this
/// handler is the second half.
///
/// Named <c>IdempotentHandler</c> rather than <c>IdempotentConsumer</c> because
/// that is also the namespace, and a type sharing its namespace's name makes
/// every qualified reference ambiguous.
/// </summary>
public sealed class IdempotentHandler
{
    private readonly IProcessedMessageLog log;

    /// <summary>Creates a handler backed by <paramref name="log"/>.</summary>
    public IdempotentHandler(IProcessedMessageLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        this.log = log;
    }

    /// <summary>
    /// Runs <paramref name="work"/> for <paramref name="capture"/>, unless it has
    /// already been run.
    /// </summary>
    public HandlingResult Handle(PaymentCapture capture, Func<PaymentCapture, string> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentException.ThrowIfNullOrWhiteSpace(capture.MessageId);

        if (log.TryGetEffect(capture.MessageId, out string previous))
        {
            // The same answer as last time. Returning an error instead would
            // make the sender believe the capture failed and retry for ever.
            return new HandlingResult(HandlingOutcome.Duplicate, previous);
        }

        string effect = work(capture);

        // **After**, never before. Recording first and then failing loses the
        // work silently: the redelivery is suppressed as a duplicate and the
        // account is never debited at all. The exception propagates, which is
        // what makes the message eligible for redelivery.
        log.Record(capture.MessageId, effect);

        return new HandlingResult(HandlingOutcome.Processed, effect);
    }
}
