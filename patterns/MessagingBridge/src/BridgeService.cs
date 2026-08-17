namespace MessagingBridge;

/// <summary>
/// Moves messages between two messaging systems that cannot speak to each
/// other.
///
/// Named <c>BridgeService</c> rather than <c>MessagingBridge</c> because that is
/// also the namespace, and a type sharing its namespace's name makes every
/// qualified reference ambiguous.
///
/// **Translation is not copying.** The legacy queue has no headers, so crossing
/// to the modern bus means supplying the ones it expects and recording where the
/// message came from; crossing back means discarding them and re-encoding to the
/// flat convention. A bridge that only moved bytes would leave the modern side
/// unable to tell bridged traffic from native traffic.
/// </summary>
public sealed class BridgeService
{
    /// <summary>What separates subject from body in the legacy convention.</summary>
    public const char LegacySeparator = '|';

    private readonly LegacyBroker legacy;
    private readonly ModernBus modern;

    /// <summary>Creates a bridge between <paramref name="legacy"/> and <paramref name="modern"/>.</summary>
    public BridgeService(LegacyBroker legacy, ModernBus modern)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(modern);

        this.legacy = legacy;
        this.modern = modern;
    }

    /// <summary>
    /// Moves one message from the legacy queue to the modern bus.
    /// </summary>
    /// <returns>1 if a message crossed, 0 if there was none or it could not be translated.</returns>
    public int PumpLegacyToModern()
    {
        // Peek, not take. A message that cannot be translated must be left where
        // it is: consuming it would destroy it, and the operator would have
        // nothing to inspect or replay.
        if (!legacy.TryPeek(out string raw))
        {
            return 0;
        }

        int separator = raw.IndexOf(LegacySeparator, StringComparison.Ordinal);
        if (separator <= 0)
        {
            return 0;
        }

        legacy.TryTake(out _);

        modern.Publish(new Envelope(
            raw[..separator],
            raw[(separator + 1)..],
            new Dictionary<string, string>
            {
                ["source"] = "legacy-queue",
                ["schema-version"] = "1.0",
            }));

        return 1;
    }

    /// <summary>
    /// Moves one envelope from the modern bus to the legacy queue.
    /// </summary>
    /// <returns>1 if a message crossed, 0 if there was none.</returns>
    public int PumpModernToLegacy()
    {
        if (!modern.TryReceive(out Envelope envelope))
        {
            return 0;
        }

        // Headers are dropped, because the legacy queue has nowhere to put them.
        // That loss is real and one-way, and it is the reason a bridge is a
        // migration tool rather than a permanent architecture.
        legacy.Put($"{envelope.Subject}{LegacySeparator}{envelope.Body}");
        return 1;
    }
}
