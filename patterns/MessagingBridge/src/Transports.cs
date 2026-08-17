namespace MessagingBridge;

/// <summary>
/// The old system: flat strings, no headers, no types.
///
/// A message is <c>SUBJECT|BODY</c> by convention, which is to say by nothing —
/// the queue neither knows nor enforces it, which is exactly why an
/// untranslatable message is possible at all.
/// </summary>
public sealed class LegacyBroker
{
    private readonly Queue<string> messages = new();

    /// <summary>How many are waiting.</summary>
    public int Depth => messages.Count;

    /// <summary>Puts a raw string on the queue.</summary>
    public void Put(string message) => messages.Enqueue(message);

    /// <summary>Looks at the next message without removing it.</summary>
    public bool TryPeek(out string message)
    {
        if (messages.Count == 0)
        {
            message = string.Empty;
            return false;
        }

        message = messages.Peek();
        return true;
    }

    /// <summary>Takes the next message.</summary>
    public bool TryTake(out string message)
    {
        if (messages.Count == 0)
        {
            message = string.Empty;
            return false;
        }

        message = messages.Dequeue();
        return true;
    }
}

/// <summary>A typed message on the modern bus.</summary>
/// <param name="Subject">What it is about.</param>
/// <param name="Body">Its payload.</param>
/// <param name="Headers">Metadata the modern side relies on.</param>
public readonly record struct Envelope(
    string Subject, string Body, IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// The new system: typed envelopes with headers.
///
/// It is genuinely incompatible with <see cref="LegacyBroker"/> — different
/// shape, different vocabulary, different expectations — which is what gives the
/// bridge something to do. Two instances of one transport would reduce it to a
/// copy loop.
/// </summary>
public sealed class ModernBus
{
    private readonly Queue<Envelope> envelopes = new();

    /// <summary>How many are waiting.</summary>
    public int Depth => envelopes.Count;

    /// <summary>Publishes an envelope.</summary>
    public void Publish(Envelope envelope) => envelopes.Enqueue(envelope);

    /// <summary>Takes the next envelope.</summary>
    public bool TryReceive(out Envelope envelope)
    {
        if (envelopes.Count == 0)
        {
            envelope = default;
            return false;
        }

        envelope = envelopes.Dequeue();
        return true;
    }
}
